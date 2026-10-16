using Aura.UI.Theming;
using Aura.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using Aura.UI.Interop;
using System.Runtime.InteropServices;
using Aura.UI.Surface;
using WinRT.Interop;

namespace Aura.App;

public sealed partial class MainWindow : Microsoft.UI.Xaml.Window, IAppWindow
{
    private event EventHandler? _activatedCallback;
    private event EventHandler? _closedCallback;
    private nint _handle;
    private readonly IWin32Interop _win32Interop;
    private readonly ISurfaceView _surfaceView;
    private bool _startupPrepared;
    private bool _entranceStarted;
    private bool _startupRootLoaded;
    private bool _startupFrameRendered;

    public MainWindow(ISurfaceView surfaceView, IWin32Interop win32Interop)
    {
        _win32Interop = win32Interop;
        _surfaceView = surfaceView;
        Content = surfaceView.Root;
        Title = "Aura";
        SystemBackdrop = new DesktopAcrylicBackdrop();
        ExtendsContentIntoTitleBar = true;
        var presenter = AppWindow.Presenter as OverlappedPresenter;
        presenter?.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
        if (AppWindow.Presenter is OverlappedPresenter configuredPresenter)
        {
            configuredPresenter.IsResizable = false;
            configuredPresenter.IsMinimizable = false;
            configuredPresenter.IsMaximizable = false;
        }
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

    void IAppWindow.PrepareForStartup()
    {
        _ = Handle;
        RemoveNativeWindowFrame();
        _win32Interop.ConfigureFloatingWindow(Handle);
        HideNativeWindow();
        _startupPrepared = true;
    }

    void IAppWindow.RevealAfterStartup()
    {
        if (!_startupPrepared)
        {
            throw new InvalidOperationException("Aura startup presentation was not prepared.");
        }

        if (Content is FrameworkElement root)
        {
            root.Opacity = 0;
            root.Loaded += OnStartupRootLoaded;
            if (root.IsLoaded)
            {
                _startupRootLoaded = true;
                SubscribeToStartupFrame(root);
            }
        }
        else
        {
            ShowWithoutAnimation();
        }

        StateChanged?.Invoke(this, new WindowStateChangedEventArgs(AppWindowState.Activated, true, DateTimeOffset.UtcNow));
    }

    public void ActivateFromRedirectedLaunch() => Activate();

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
        if (!_startupPrepared)
        {
            RemoveNativeWindowFrame();
            _win32Interop.ConfigureFloatingWindow(Handle);
        }
        var state = args.WindowActivationState == WindowActivationState.Deactivated
            ? AppWindowState.Deactivated
            : AppWindowState.Activated;

        StateChanged?.Invoke(this, new WindowStateChangedEventArgs(state, true, DateTimeOffset.UtcNow));
    }

    private void OnStartupRootLoaded(object sender, RoutedEventArgs args)
    {
        if (sender is FrameworkElement root)
        {
            root.Loaded -= OnStartupRootLoaded;
            _startupRootLoaded = true;
            SubscribeToStartupFrame(root);
        }
    }

    private void SubscribeToStartupFrame(FrameworkElement root)
    {
        if (_startupFrameRendered)
        {
            return;
        }

        Microsoft.UI.Xaml.Media.CompositionTarget.Rendered += OnStartupFrameRendered;
    }

    private void OnStartupFrameRendered(object? sender, object args)
    {
        Microsoft.UI.Xaml.Media.CompositionTarget.Rendered -= OnStartupFrameRendered;
        _startupFrameRendered = true;
        ShowNativeWindow();
        if (Content is FrameworkElement root && _startupRootLoaded)
        {
            BeginStartupEntranceWhenReady(root);
        }
    }

    private void BeginStartupEntranceWhenReady(FrameworkElement root)
    {
        if (_entranceStarted || !_startupFrameRendered)
        {
            return;
        }

        _entranceStarted = true;
        root.Opacity = 1;
        _surfaceView.BeginStartupEntrance();
        try
        {
            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            var fade = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(220)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.QuadraticEase
                {
                    EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut
                }
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(fade, root);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(fade, "Opacity");
            storyboard.Children.Add(fade);
            storyboard.Completed += (_, _) => root.Opacity = 1;
            storyboard.Begin();
        }
        catch
        {
            root.Opacity = 1;
        }
    }

    private void ShowWithoutAnimation()
    {
        ShowNativeWindow();
        if (Content is FrameworkElement root)
        {
            root.Opacity = 1;
        }
    }

    private void HideNativeWindow()
    {
        const int SwHide = 0;
        _ = ShowWindow(Handle, SwHide);
    }

    private void ShowNativeWindow()
    {
        const int SwShowNoActivate = 4;
        _ = ShowWindow(Handle, SwShowNoActivate);
    }

    private void RemoveNativeWindowFrame()
    {
        const int GwlStyle = -16;
        const long FrameStyles = 0x00C00000L | 0x00040000L | 0x00020000L | 0x00010000L | 0x00080000L | 0x00040000L | 0x00020000L;
        const uint SwpNoMove = 0x0002;
        const uint SwpNoSize = 0x0001;
        const uint SwpNoZOrder = 0x0004;
        const uint SwpNoActivate = 0x0010;
        const uint SwpFrameChanged = 0x0020;

        var hwnd = Handle;
        var style = GetWindowLongPtr(hwnd, GwlStyle).ToInt64();
        SetWindowLongPtr(hwnd, GwlStyle, new nint(style & ~FrameStyles));
        if (!SetWindowPos(hwnd, 0, 0, 0, 0, 0,
                SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpFrameChanged))
        {
            throw new InvalidOperationException("Unable to apply the borderless Aura window style.");
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint hwnd, int command);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr(nint hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _closedCallback?.Invoke(this, EventArgs.Empty);
        StateChanged?.Invoke(this, new WindowStateChangedEventArgs(AppWindowState.Closed, false, DateTimeOffset.UtcNow));
    }
}
