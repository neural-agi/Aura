namespace Aura.UI.Interop;

public readonly record struct MonitorSnapshot(
    string DeviceName,
    int Left,
    int Top,
    int Right,
    int Bottom,
    int WorkLeft,
    int WorkTop,
    int WorkRight,
    int WorkBottom,
    uint Dpi,
    bool IsPrimary);

