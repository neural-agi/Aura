using Aura.UI.Interop;
using Aura.UI.Monitoring;
using Aura.UI.Theming;
using Aura.UI.Surface;
using Aura.Runtime;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aura.UI.Windowing;

public sealed class WindowManager : IWindowManager
{
    private readonly IAppWindowFactory _windowFactory;
    private readonly IWin32Interop _win32Interop;
    private readonly IMonitorService _monitorService;
    private readonly IThemeService _themeService;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<WindowManager> _logger;
    private readonly ISurfaceManager _surfaceManager;
    private readonly IStateMachine _stateMachine;
    private readonly IClipboardWindowListener _clipboardListener;
    private readonly INotificationAreaIcon _notificationAreaIcon;
    private readonly object _gate = new();

    private IAppWindow? _activeWindow;
    private bool _started;
    private int _shutdownRequested;

    public WindowManager(
        IAppWindowFactory windowFactory,
        IWin32Interop win32Interop,
        IMonitorService monitorService,
        IThemeService themeService,
        IHostApplicationLifetime lifetime,
        ILogger<WindowManager> logger,
        ISurfaceManager surfaceManager,
        IStateMachine stateMachine,
        IClipboardWindowListener clipboardListener,
        INotificationAreaIcon notificationAreaIcon)
    {
        _windowFactory = windowFactory;
        _win32Interop = win32Interop;
        _monitorService = monitorService;
        _themeService = themeService;
        _lifetime = lifetime;
        _logger = logger;
        _surfaceManager = surfaceManager;
        _stateMachine = stateMachine;
        _clipboardListener = clipboardListener;
        _notificationAreaIcon = notificationAreaIcon;
    }

    public IAppWindow? ActiveWindow
    {
        get { lock (_gate) return _activeWindow; }
    }

    public event EventHandler<WindowStateChangedEventArgs>? WindowStateChanged;

    public event EventHandler? ShutdownRequested;

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("WindowManager.StartAsync entered.");
        IAppWindow window;

        lock (_gate)
        {
            if (_started)
            {
                return ValueTask.CompletedTask;
            }

            _started = true;
            _shutdownRequested = 0;
            window = _windowFactory.Create();
            _activeWindow = window;
        }
        _logger.LogInformation("Window created and assigned as active window.");

        _win32Interop.EnsurePerMonitorDpiAwareness();
        window.StateChanged += OnWindowStateChanged;
        window.Closed += OnWindowClosed;
        window.PrepareForStartup();
        _themeService.Attach(window);
        _monitorService.Attach(window);
        _clipboardListener.Start();
        _clipboardListener.Attach(window.Handle);
        _notificationAreaIcon.Attach(window.Handle);
        _surfaceManager.Attach(window.Handle);
        _stateMachine.TryTransition(AuraState.Surfacing);
        _stateMachine.TryTransition(AuraState.Compact);
        window.RevealAfterStartup();
        _logger.LogInformation("Aura window shell started.");
        return ValueTask.CompletedTask;
    }

    public void RequestApplicationExit()
    {
        IAppWindow? window;
        lock (_gate) window = _activeWindow;
        if (window is not null) window.Close();
    }

    public ValueTask ShutdownAsync(CancellationToken cancellationToken = default)
    {
        IAppWindow? window;

        lock (_gate)
        {
            if (!_started)
            {
                return ValueTask.CompletedTask;
            }

            _started = false;
            window = _activeWindow;
        }

        try
        {
            if (window is not null)
            {
                window.Close();
            }
        }
        finally
        {
            CleanupWindow(window);
        }

        _logger.LogInformation("Aura window shell stopped.");
        return ValueTask.CompletedTask;
    }

    private void OnWindowStateChanged(object? sender, WindowStateChangedEventArgs e)
    {
        WindowStateChanged?.Invoke(this, e);
        _logger.LogDebug("Window state changed to {State} at {OccurredAtUtc}.", e.State, e.OccurredAtUtc);
        _monitorService.Refresh();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (Interlocked.Exchange(ref _shutdownRequested, 1) == 1)
        {
            return;
        }

        ShutdownRequested?.Invoke(this, EventArgs.Empty);
        _lifetime.StopApplication();
        CleanupWindow(_activeWindow);
    }

    private void CleanupWindow(IAppWindow? window)
    {
        if (window is null)
        {
            return;
        }

        window.StateChanged -= OnWindowStateChanged;
        window.Closed -= OnWindowClosed;
        _clipboardListener.Stop();
        _notificationAreaIcon.Dispose();
        _monitorService.Detach();
        _themeService.Detach();

        lock (_gate)
        {
            if (ReferenceEquals(_activeWindow, window))
            {
                _activeWindow = null;
            }
        }
    }
}
