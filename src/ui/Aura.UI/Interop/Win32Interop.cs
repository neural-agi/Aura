using System.Runtime.InteropServices;

namespace Aura.UI.Interop;

public sealed class Win32Interop : IWin32Interop
{
    private const uint MonitorDefaultToNearest = 2;
    private const uint MonitorPrimary = 1;
    private const uint SetWindowPositionNoActivate = 0x0010;
    private const uint SetWindowPositionNoZOrder = 0x0004;
    private const uint SetWindowPositionNoMove = 0x0002;
    private const uint SetWindowPositionNoSize = 0x0001;
    private const uint SetWindowPositionFrameChanged = 0x0020;
    private const int ExtendedWindowStyleIndex = -20;
    private const long ExtendedToolWindow = 0x00000080L;
    private static readonly nint TopmostWindow = new(-1);
    private const int RegionCornerDiameter = 48;
    private static readonly nint PerMonitorAwareV2 = new(-4);
    private bool _dpiAware;

    public void EnsurePerMonitorDpiAwareness()
    {
        if (_dpiAware)
        {
            return;
        }

        _ = SetProcessDpiAwarenessContext(PerMonitorAwareV2);
        _dpiAware = true;
    }

    public uint GetDpiForWindow(nint windowHandle)
        => GetDpiForWindowInternal(windowHandle);

    public MonitorSnapshot GetMonitorSnapshot(nint windowHandle)
    {
        var monitor = MonitorFromWindow(windowHandle, MonitorDefaultToNearest);
        var info = new MonitorInfo
        {
            Size = (uint)Marshal.SizeOf<MonitorInfo>()
        };

        if (!GetMonitorInfo(monitor, ref info))
        {
            throw new InvalidOperationException("Unable to query monitor information.");
        }

        return new MonitorSnapshot(
            info.DeviceName,
            info.Monitor.Left,
            info.Monitor.Top,
            info.Monitor.Right,
            info.Monitor.Bottom,
            info.Work.Left,
            info.Work.Top,
            info.Work.Right,
            info.Work.Bottom,
            GetDpiForWindow(windowHandle),
            info.Flags == MonitorPrimary);
    }

    public void SetWindowBounds(nint windowHandle, int left, int top, int width, int height)
    {
        if (!SetWindowPos(windowHandle, 0, left, top, width, height,
                SetWindowPositionNoActivate | SetWindowPositionNoZOrder))
        {
            throw new InvalidOperationException("Unable to position the Aura window.");
        }

        ApplyRoundedRegion(windowHandle, width, height);
    }

    public void ConfigureFloatingWindow(nint windowHandle)
    {
        var extendedStyle = GetWindowLongPtr(windowHandle, ExtendedWindowStyleIndex).ToInt64();
        SetWindowLongPtr(windowHandle, ExtendedWindowStyleIndex, new nint(extendedStyle | ExtendedToolWindow));
        if (!SetWindowPos(windowHandle, TopmostWindow, 0, 0, 0, 0,
                SetWindowPositionNoMove | SetWindowPositionNoSize | SetWindowPositionNoActivate | SetWindowPositionFrameChanged))
        {
            throw new InvalidOperationException("Unable to configure Aura as a floating tool window.");
        }

        var bounds = new Rect();
        if (!GetWindowRect(windowHandle, ref bounds))
        {
            throw new InvalidOperationException("Unable to read the Aura window dimensions.");
        }

        ApplyRoundedRegion(windowHandle, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top);
    }

    private static void ApplyRoundedRegion(nint windowHandle, int width, int height)
    {
        var diameter = Math.Min(RegionCornerDiameter, Math.Min(width, height));
        var region = CreateRoundRectRgn(0, 0, width + 1, height + 1, diameter, diameter);
        if (region == 0 || SetWindowRgn(windowHandle, region, true) == 0)
        {
            if (region != 0)
            {
                DeleteObject(region);
            }

            throw new InvalidOperationException("Unable to apply the rounded Aura window region.");
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr(nint hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern nint CreateRoundRectRgn(int left, int top, int right, int bottom, int ellipseWidth, int ellipseHeight);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowRgn(nint hwnd, nint region, [MarshalAs(UnmanagedType.Bool)] bool redraw);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(nint objectHandle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessDpiAwarenessContext(nint dpiAwarenessContext);

    [DllImport("user32.dll", EntryPoint = "GetDpiForWindow", SetLastError = true)]
    private static extern uint GetDpiForWindowInternal(nint hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint MonitorFromWindow(nint hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint hMonitor, ref MonitorInfo lpmi);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint hwnd, ref Rect rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        public uint Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }
}
