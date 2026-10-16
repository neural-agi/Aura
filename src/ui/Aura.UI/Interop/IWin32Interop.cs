namespace Aura.UI.Interop;

public interface IWin32Interop
{
    void EnsurePerMonitorDpiAwareness();

    uint GetDpiForWindow(nint windowHandle);

    MonitorSnapshot GetMonitorSnapshot(nint windowHandle);

    void SetWindowBounds(nint windowHandle, int left, int top, int width, int height);
}

