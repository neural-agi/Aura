using Aura.UI.Windowing;

namespace Aura.UI.Theming;

public interface IThemeService
{
    AppThemeMode RequestedMode { get; }

    AppThemeMode EffectiveMode { get; }

    event EventHandler<AppThemeMode>? ThemeChanged;

    void SetMode(AppThemeMode mode);

    void Attach(IAppWindow window);

    void Detach();
}

