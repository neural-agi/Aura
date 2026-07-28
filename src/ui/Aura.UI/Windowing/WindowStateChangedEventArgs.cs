namespace Aura.UI.Windowing;

public sealed class WindowStateChangedEventArgs : EventArgs
{
    public WindowStateChangedEventArgs(AppWindowState state, bool isVisible, DateTimeOffset occurredAtUtc)
    {
        State = state;
        IsVisible = isVisible;
        OccurredAtUtc = occurredAtUtc;
    }

    public AppWindowState State { get; }

    public bool IsVisible { get; }

    public DateTimeOffset OccurredAtUtc { get; }
}

