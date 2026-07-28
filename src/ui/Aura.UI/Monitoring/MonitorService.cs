using Aura.UI.Interop;
using Aura.UI.Windowing;
using Microsoft.Extensions.Logging;

namespace Aura.UI.Monitoring;

public sealed class MonitorService : IMonitorService
{
    private readonly IWin32Interop _win32Interop;
    private readonly ILogger<MonitorService> _logger;
    private readonly object _gate = new();

    private IAppWindow? _window;
    private MonitorSnapshot? _currentMonitor;

    public MonitorService(IWin32Interop win32Interop, ILogger<MonitorService> logger)
    {
        _win32Interop = win32Interop;
        _logger = logger;
    }

    public MonitorSnapshot? CurrentMonitor
    {
        get { lock (_gate) return _currentMonitor; }
    }

    public event EventHandler<MonitorSnapshot?>? CurrentMonitorChanged;

    public void Attach(IAppWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        lock (_gate)
        {
            if (_window is not null)
            {
                _window.StateChanged -= OnWindowStateChanged;
                _window = null;
            }

            _window = window;
            _window.StateChanged += OnWindowStateChanged;
        }

        Refresh();
    }

    public void Detach()
    {
        lock (_gate)
        {
            if (_window is null)
            {
                return;
            }

            _window.StateChanged -= OnWindowStateChanged;
            _window = null;
            _currentMonitor = null;
        }

        CurrentMonitorChanged?.Invoke(this, null);
    }

    public void Refresh()
    {
        IAppWindow? window;
        lock (_gate)
        {
            window = _window;
        }

        if (window is null || window.Handle == 0)
        {
            SetCurrentMonitor(null);
            return;
        }

        try
        {
            SetCurrentMonitor(_win32Interop.GetMonitorSnapshot(window.Handle));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to refresh monitor snapshot.");
            SetCurrentMonitor(null);
        }
    }

    private void OnWindowStateChanged(object? sender, WindowStateChangedEventArgs e)
    {
        Refresh();
    }

    private void SetCurrentMonitor(MonitorSnapshot? monitor)
    {
        lock (_gate)
        {
            _currentMonitor = monitor;
        }

        CurrentMonitorChanged?.Invoke(this, monitor);
    }
}

