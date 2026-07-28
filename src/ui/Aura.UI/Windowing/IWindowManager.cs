namespace Aura.UI.Windowing;

public interface IWindowManager
{
    IAppWindow? ActiveWindow { get; }

    event EventHandler<WindowStateChangedEventArgs> WindowStateChanged;

    event EventHandler? ShutdownRequested;

    ValueTask StartAsync(CancellationToken cancellationToken = default);

    ValueTask ShutdownAsync(CancellationToken cancellationToken = default);
}

