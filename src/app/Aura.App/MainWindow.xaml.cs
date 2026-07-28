using Aura.UI.Theming;
using Aura.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace Aura.App;

public sealed partial class MainWindow : Microsoft.UI.Xaml.Window, IAppWindow
{
    private event EventHandler? _activatedCallback;
    private event EventHandler? _closedCallback;
    private nint _handle;

    public MainWindow()
    {
        InitializeComponent();
        Title = "Aura";
        Activated += OnActivated;
        Closed += OnClosed;
    }

    public nint Handle
    {
        get
        {
            if (_handle == 0)
            {
                _handle = WindowNative.GetWindowHandle(this);
            }

            return _handle;
        }
    }

    public event EventHandler<WindowStateChangedEventArgs>? StateChanged;

    event EventHandler IAppWindow.Activated
    {
        add => _activatedCallback += value;
        remove => _activatedCallback -= value;
    }

    event EventHandler IAppWindow.Closed
    {
        add => _closedCallback += value;
        remove => _closedCallback -= value;
    }

    void IAppWindow.Activate() => Activate();

    void IAppWindow.Close() => Close();

    void IAppWindow.ApplyTheme(AppThemeMode mode)
    {
        if (Content is FrameworkElement root)
        {
            root.RequestedTheme = mode switch
            {
                AppThemeMode.Light => ElementTheme.Light,
                AppThemeMode.Dark => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        _activatedCallback?.Invoke(this, EventArgs.Empty);
        _ = Handle;
        var state = args.WindowActivationState == WindowActivationState.Deactivated
            ? AppWindowState.Deactivated
            : AppWindowState.Activated;

        StateChanged?.Invoke(this, new WindowStateChangedEventArgs(state, true, DateTimeOffset.UtcNow));
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _closedCallback?.Invoke(this, EventArgs.Empty);
        StateChanged?.Invoke(this, new WindowStateChangedEventArgs(AppWindowState.Closed, false, DateTimeOffset.UtcNow));
    }
}
