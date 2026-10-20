import ctypes
import sys
import time

user32 = ctypes.WinDLL("user32", use_last_error=True)

class POINT(ctypes.Structure):
    _fields_ = [("x", ctypes.c_long), ("y", ctypes.c_long)]

class RECT(ctypes.Structure):
    _fields_ = [("left", ctypes.c_long), ("top", ctypes.c_long), ("right", ctypes.c_long), ("bottom", ctypes.c_long)]

class WNDCLASS(ctypes.Structure):
    _fields_ = [("style", ctypes.c_uint), ("lpfnWndProc", ctypes.c_void_p), ("cbClsExtra", ctypes.c_int), ("cbWndExtra", ctypes.c_int), ("hInstance", ctypes.c_void_p), ("hIcon", ctypes.c_void_p), ("hCursor", ctypes.c_void_p), ("hbrBackground", ctypes.c_void_p), ("lpszMenuName", ctypes.c_wchar_p), ("lpszClassName", ctypes.c_wchar_p)]

user32.SetCursorPos.argtypes = [ctypes.c_int, ctypes.c_int]
user32.mouse_event.argtypes = [ctypes.c_uint, ctypes.c_uint, ctypes.c_uint, ctypes.c_uint, ctypes.c_size_t]

pid = int(sys.argv[1])
hwnds = []
CB = ctypes.WINFUNCTYPE(ctypes.c_bool, ctypes.c_void_p, ctypes.c_void_p)
@CB
def enum(hwnd, _):
    window_pid = ctypes.c_ulong()
    user32.GetWindowThreadProcessId(hwnd, ctypes.byref(window_pid))
    title = ctypes.create_unicode_buffer(256)
    user32.GetWindowTextW(hwnd, title, 256)
    if window_pid.value == pid and title.value == "Aura" and user32.IsWindowVisible(hwnd):
        hwnds.append(hwnd)
    return True
user32.EnumWindows(enum, None)
if not hwnds:
    raise SystemExit("No visible Aura HWND")
hwnd = hwnds[0]
rect = RECT()
user32.GetWindowRect(hwnd, ctypes.byref(rect))
old = POINT()
user32.GetCursorPos(ctypes.byref(old))
user32.SetCursorPos(rect.left + 48, rect.top + 42)
time.sleep(0.2)
user32.mouse_event(0x0002, 0, 0, 0, 0)
user32.mouse_event(0x0004, 0, 0, 0, 0)
time.sleep(1.5)
user32.SetCursorPos(old.x, old.y)
print("Clicked Aura at", rect.left + 48, rect.top + 42)
print("HWND", int(hwnd))
