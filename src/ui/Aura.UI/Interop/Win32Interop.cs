using System.Runtime.InteropServices;

namespace Aura.UI.Interop;

public sealed class Win32Interop : IWin32Interop
{
    private const uint MonitorDefaultToNearest = 2;
    private const uint MonitorPrimary = 1;
    private const uint SetWindowPositionNoActivate = 0x0010;
    private const uint SetWindowPositionNoZOrder = 0x0004;
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
    }

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
