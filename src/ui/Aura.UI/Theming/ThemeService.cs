using Aura.UI.Windowing;
using Microsoft.Extensions.Logging;

namespace Aura.UI.Theming;

public sealed class ThemeService : IThemeService
{
    private readonly ILogger<ThemeService> _logger;
    private readonly object _gate = new();
    private IAppWindow? _window;
    private AppThemeMode _requestedMode = AppThemeMode.System;
    private AppThemeMode _effectiveMode = AppThemeMode.System;

    public ThemeService(ILogger<ThemeService> logger)
    {
        _logger = logger;
    }

    public AppThemeMode RequestedMode
    {
        get { lock (_gate) return _requestedMode; }
    }

    public AppThemeMode EffectiveMode
    {
        get { lock (_gate) return _effectiveMode; }
    }

    public event EventHandler<AppThemeMode>? ThemeChanged;

    public void SetMode(AppThemeMode mode)
    {
        lock (_gate)
        {
            _requestedMode = mode;
            _effectiveMode = mode;
        }

        ApplyCurrentMode();
        ThemeChanged?.Invoke(this, mode);
    }

    public void Attach(IAppWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        lock (_gate)
        {
            _window = window;
        }

        ApplyCurrentMode();
    }

    public void Detach()
    {
        lock (_gate)
        {
            _window = null;
        }
    }

    private void ApplyCurrentMode()
    {
        IAppWindow? window;
        AppThemeMode mode;
        lock (_gate)
        {
            window = _window;
            mode = _requestedMode;
        }

        if (window is null)
        {
            return;
        }

        try
        {
            window.ApplyTheme(mode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to apply theme mode {ThemeMode}.", mode);
        }
    }
}

