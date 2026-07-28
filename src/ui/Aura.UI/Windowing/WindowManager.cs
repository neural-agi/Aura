using Aura.UI.Interop;
using Aura.UI.Monitoring;
using Aura.UI.Theming;
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
        ILogger<WindowManager> logger)
    {
        _windowFactory = windowFactory;
        _win32Interop = win32Interop;
        _monitorService = monitorService;
        _themeService = themeService;
        _lifetime = lifetime;
        _logger = logger;
    }

    public IAppWindow? ActiveWindow
    {
        get { lock (_gate) return _activeWindow; }
    }

    public event EventHandler<WindowStateChangedEventArgs>? WindowStateChanged;

    public event EventHandler? ShutdownRequested;

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
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

        _win32Interop.EnsurePerMonitorDpiAwareness();
        _themeService.Attach(window);
        _monitorService.Attach(window);
        window.StateChanged += OnWindowStateChanged;
        window.Closed += OnWindowClosed;
        window.Activate();
        _logger.LogInformation("Aura window shell started.");
        return ValueTask.CompletedTask;
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
