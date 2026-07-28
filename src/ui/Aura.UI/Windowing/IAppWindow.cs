using Aura.UI.Theming;

namespace Aura.UI.Windowing;

public interface IAppWindow
{
    event EventHandler<WindowStateChangedEventArgs> StateChanged;

    event EventHandler Activated;

    event EventHandler Closed;

    nint Handle { get; }

    void Activate();

    void Close();

    void ApplyTheme(AppThemeMode mode);
}

