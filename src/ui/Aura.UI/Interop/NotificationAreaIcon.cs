using System.Runtime.InteropServices;

namespace Aura.UI.Interop;

public interface INotificationAreaIcon : IDisposable
{
    event EventHandler? ExitRequested;
    void Attach(nint windowHandle);
}

public sealed class NotificationAreaIcon : INotificationAreaIcon
{
    private const uint TrayCallbackMessage = 0x8000 + 0x42;
    private const uint WmContextMenu = 0x007B;
    private const uint WmRightButtonUp = 0x0205;
    private const uint WmLeftButtonDoubleClick = 0x0203;
    private const uint NifMessage = 0x0001;
    private const uint NifIcon = 0x0002;
    private const uint NifTip = 0x0004;
    private const uint NimAdd = 0;
    private const uint NimDelete = 2;
    private const uint NimSetVersion = 4;
    private const uint NotifyIconVersion4 = 4;
    private const uint TrackReturnCommand = 0x0100;
    private const uint MenuRightButton = 0x0002;
    private const uint ExitCommandId = 1;
    private static readonly nint SubclassId = new(0x41555254);

    private readonly NativeSubclassProcedure _callback;
    private nint _windowHandle;
    private nint _iconHandle;
    private bool _registered;
    private bool _subclassed;

    public NotificationAreaIcon() => _callback = OnWindowMessage;

    public event EventHandler? ExitRequested;

    public void Attach(nint windowHandle)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowHandle);
        if (_windowHandle == windowHandle && _registered) return;
        DisposeRegistration();
        _windowHandle = windowHandle;
        _iconHandle = LoadIcon(0, new nint(32512));
        _subclassed = SetWindowSubclass(windowHandle, _callback, unchecked((nuint)SubclassId), 0);
        if (!_subclassed)
        {
            _windowHandle = 0;
            throw new InvalidOperationException("Unable to attach the Aura notification-area action to its window.");
        }

        var data = CreateIconData(windowHandle, _iconHandle);
        if (!Shell_NotifyIcon(NimAdd, ref data))
        {
            DisposeRegistration();
            throw new InvalidOperationException("Unable to add Aura to the Windows notification area.");
        }
        _registered = true;
        data.TimeoutOrVersion = NotifyIconVersion4;
        if (!Shell_NotifyIcon(NimSetVersion, ref data))
        {
            DisposeRegistration();
            throw new InvalidOperationException("Unable to register Aura's notification-area callback.");
        }
    }

    private nint OnWindowMessage(nint hwnd, uint message, nint wParam, nint lParam, nuint subclassId, nuint referenceData)
    {
        if (message == TrayCallbackMessage)
        {
            var callbackMessage = unchecked((uint)(lParam.ToInt64() & 0xFFFF));
            if (callbackMessage is WmContextMenu or WmRightButtonUp or WmLeftButtonDoubleClick)
                ShowContextMenu(hwnd);
        }
        return DefSubclassProc(hwnd, message, wParam, lParam);
    }

    private void ShowContextMenu(nint hwnd)
    {
        var menu = CreatePopupMenu();
        if (menu == 0) return;
        try
        {
            _ = AppendMenu(menu, 0, ExitCommandId, "Exit Aura");
            _ = SetForegroundWindow(hwnd);
            var point = new Point();
            _ = GetCursorPos(ref point);
            var command = TrackPopupMenu(menu, TrackReturnCommand | MenuRightButton, point.X, point.Y, 0, hwnd, 0);
            if (command == ExitCommandId) ExitRequested?.Invoke(this, EventArgs.Empty);
            _ = PostMessage(hwnd, 0, 0, 0);
        }
        finally
        {
            _ = DestroyMenu(menu);
        }
    }

    private static NotifyIconData CreateIconData(nint hwnd, nint icon) => new()
    {
        Size = (uint)Marshal.SizeOf<NotifyIconData>(),
        WindowHandle = hwnd,
        Id = 1,
        Flags = NifMessage | NifIcon | NifTip,
        CallbackMessage = TrayCallbackMessage,
        Icon = icon,
        Tip = "Aura"
    };

    public void Dispose()
    {
        DisposeRegistration();
        GC.SuppressFinalize(this);
    }

    private void DisposeRegistration()
    {
        if (_registered)
        {
            var data = CreateIconData(_windowHandle, _iconHandle);
            _ = Shell_NotifyIcon(NimDelete, ref data);
            _registered = false;
        }
        if (_subclassed)
        {
            _ = RemoveWindowSubclass(_windowHandle, _callback, unchecked((nuint)SubclassId));
            _subclassed = false;
        }
        _windowHandle = 0;
        _iconHandle = 0;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint NativeSubclassProcedure(nint hwnd, uint message, nint wParam, nint lParam, nuint subclassId, nuint referenceData);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint Size;
        public nint WindowHandle;
        public uint Id;
        public uint Flags;
        public uint CallbackMessage;
        public nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State;
        public uint StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint TimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags;
        public Guid ItemGuid;
        public nint BalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point { public int X; public int Y; }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool Shell_NotifyIcon(uint message, ref NotifyIconData data);
    [DllImport("comctl32.dll", SetLastError = true)] private static extern bool SetWindowSubclass(nint hwnd, NativeSubclassProcedure procedure, nuint subclassId, nuint referenceData);
    [DllImport("comctl32.dll", SetLastError = true)] private static extern bool RemoveWindowSubclass(nint hwnd, NativeSubclassProcedure procedure, nuint subclassId);
    [DllImport("comctl32.dll")] private static extern nint DefSubclassProc(nint hwnd, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint LoadIcon(nint instance, nint iconName);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool AppendMenu(nint menu, uint flags, uint id, string text);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint TrackPopupMenu(nint menu, uint flags, int x, int y, int reserved, nint hwnd, nint rect);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetCursorPos(ref Point point);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool PostMessage(nint hwnd, uint message, nint wParam, nint lParam);
}
