import ctypes
import json
import sys

user32 = ctypes.WinDLL("user32", use_last_error=True)
gdi32 = ctypes.WinDLL("gdi32", use_last_error=True)

class RECT(ctypes.Structure):
    _fields_ = [("left", ctypes.c_long), ("top", ctypes.c_long), ("right", ctypes.c_long), ("bottom", ctypes.c_long)]

WNDENUMPROC = ctypes.WINFUNCTYPE(ctypes.c_bool, ctypes.c_void_p, ctypes.c_void_p)


def enumerate_windows(pid):
    result = []

    @WNDENUMPROC
    def callback(hwnd, _):
        window_pid = ctypes.c_ulong()
        user32.GetWindowThreadProcessId(hwnd, ctypes.byref(window_pid))
        if window_pid.value != pid:
            return True
        title = ctypes.create_unicode_buffer(512)
        cls = ctypes.create_unicode_buffer(256)
        user32.GetWindowTextW(hwnd, title, len(title))
        user32.GetClassNameW(hwnd, cls, len(cls))
        rect = RECT()
        user32.GetWindowRect(hwnd, ctypes.byref(rect))
        region = gdi32.CreateRectRgn(0, 0, 0, 0)
        region_type = user32.GetWindowRgn(hwnd, region)
        region_rect = RECT()
        gdi32.GetRgnBox(region, ctypes.byref(region_rect))
        region_data_size = gdi32.GetRegionData(region, 0, None)
        region_data = ctypes.create_string_buffer(region_data_size)
        gdi32.GetRegionData(region, region_data_size, region_data)
        scan_count = ctypes.cast(region_data, ctypes.POINTER(ctypes.c_uint32))[2]
        scan_rectangles = []
        if scan_count:
            header_size = ctypes.sizeof(ctypes.c_uint32) * 4
            rectangle_array = ctypes.cast(ctypes.addressof(region_data) + header_size, ctypes.POINTER(RECT))
            scan_rectangles = [[rectangle_array[i].left, rectangle_array[i].top, rectangle_array[i].right, rectangle_array[i].bottom] for i in range(scan_count)]
        gdi32.DeleteObject(region)
        result.append({
            "hwnd": int(hwnd), "class": cls.value, "title": title.value,
            "visible": bool(user32.IsWindowVisible(hwnd)),
            "rect": [rect.left, rect.top, rect.right, rect.bottom],
            "style": f"0x{user32.GetWindowLongPtrW(hwnd, -16) & 0xffffffff:08X}",
            "exstyle": f"0x{user32.GetWindowLongPtrW(hwnd, -20) & 0xffffffff:08X}",
            "region_type": int(region_type),
            "region_bounds": [region_rect.left, region_rect.top, region_rect.right, region_rect.bottom],
            "region_scan_count": int(scan_count),
            "region_scan_rectangles": scan_rectangles,
            "topmost": bool(user32.GetWindowLongPtrW(hwnd, -20) & 0x00000008),
        })
        return True

    user32.EnumWindows(callback, None)
    return result

if __name__ == "__main__":
    import time
    pid = int(sys.argv[1])
    windows = []
    for _ in range(100):
        windows = enumerate_windows(pid)
        if any(window["title"] == "Aura" for window in windows):
            break
        time.sleep(0.1)
    print(json.dumps(windows, indent=2))
