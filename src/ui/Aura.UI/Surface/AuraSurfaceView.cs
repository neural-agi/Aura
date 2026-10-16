using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Aura.UI.Interaction;

namespace Aura.UI.Surface;

public sealed class AuraSurfaceView : ISurfaceView
{
    private readonly Border _surface;
    private readonly Ellipse _entranceHighlight;
    private readonly TextBlock _status;
    private readonly TextBlock _context;
    private readonly Button _expand;
    private readonly Button _collapse;
    private readonly Button _interactive;
    private readonly Button _dismiss;
    private readonly Button _recover;

    public AuraSurfaceView()
    {
        _status = new TextBlock { Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), FontSize = 16 };
        _context = new TextBlock { Foreground = new SolidColorBrush(Microsoft.UI.Colors.LightGray), FontSize = 12 };
        _expand = CreateButton("Expand", "Expand Aura surface", InteractionIntent.Expand);
        _collapse = CreateButton("Collapse", "Collapse Aura surface", InteractionIntent.Collapse);
        _interactive = CreateButton("Open actions", "Open Aura surface actions", InteractionIntent.EnterInteractive);
        _dismiss = CreateButton("Dismiss", "Dismiss Aura surface", InteractionIntent.Dismiss);
        _recover = CreateButton("Recover", "Recover Aura surface", InteractionIntent.Recover);

        var content = new StackPanel { Spacing = 6, Padding = new Thickness(18, 12, 18, 12) };
        content.Children.Add(_status);
        content.Children.Add(_context);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        actions.Children.Add(_expand);
        actions.Children.Add(_collapse);
        actions.Children.Add(_interactive);
        actions.Children.Add(_dismiss);
        actions.Children.Add(_recover);
        content.Children.Add(actions);

        var glass = new Grid();
        glass.SizeChanged += (_, args) =>
        {
            glass.Clip = new RectangleGeometry
            {
                Rect = new Windows.Foundation.Rect(0, 0, args.NewSize.Width, args.NewSize.Height)
            };
        };
        var glassFill = new Border
        {
            Background = new AcrylicBrush
            {
                TintColor = Microsoft.UI.ColorHelper.FromArgb(224, 18, 28, 42),
                TintOpacity = 0.82
            },
            BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(110, 235, 245, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(24),
            Child = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var highlightBrush = new RadialGradientBrush
        {
            Center = new Windows.Foundation.Point(0.5, 0.5),
            GradientOrigin = new Windows.Foundation.Point(0.5, 0.5),
            RadiusX = 0.5,
            RadiusY = 0.5
        };
        highlightBrush.GradientStops.Add(new GradientStop { Color = Microsoft.UI.ColorHelper.FromArgb(72, 215, 238, 255), Offset = 0 });
        highlightBrush.GradientStops.Add(new GradientStop { Color = Microsoft.UI.ColorHelper.FromArgb(0, 215, 238, 255), Offset = 1 });
        _entranceHighlight = new Ellipse
        {
            Width = 24,
            Height = 24,
            Fill = highlightBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            Opacity = 0
        };
        glass.Children.Add(glassFill);
        glass.Children.Add(_entranceHighlight);
        _surface = new Border
        {
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(24),
            Child = glass,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        Root = _surface;
        Render(new SurfacePresentationState(SurfacePresentationMode.Hidden, false, string.Empty, string.Empty));
    }

    public UIElement Root { get; }
    public event EventHandler<InteractionIntentRequestedEventArgs>? InteractionRequested;

    public void Render(SurfacePresentationState state)
    {
        _status.Text = state.StatusText;
        _context.Text = state.ContextText;

        // Keep a compact recovery affordance available after dismissal; the dismissed
        // presentation is otherwise reduced to the Recover action only.
        Root.Visibility = state.IsVisible || state.Mode == SurfacePresentationMode.Dismissed
            ? Visibility.Visible
            : Visibility.Collapsed;
        _expand.Visibility = state.Mode is SurfacePresentationMode.Compact or SurfacePresentationMode.Eligible ? Visibility.Visible : Visibility.Collapsed;
        _interactive.Visibility = state.Mode == SurfacePresentationMode.Expanded ? Visibility.Visible : Visibility.Collapsed;
        _dismiss.Visibility = state.IsVisible ? Visibility.Visible : Visibility.Collapsed;
        _recover.Visibility = state.Mode is SurfacePresentationMode.Dismissed or SurfacePresentationMode.Hidden ? Visibility.Visible : Visibility.Collapsed;
        _collapse.Visibility = state.Mode is SurfacePresentationMode.Expanded or SurfacePresentationMode.Interactive ? Visibility.Visible : Visibility.Collapsed;
    }

    public void BeginStartupEntrance()
    {
        PlayEntranceHighlight();
    }

    private void PlayEntranceHighlight()
    {
        try
        {
            var storyboard = new Storyboard();
            var size = new DoubleAnimation
            {
                From = 24,
                To = 300,
                Duration = new Duration(TimeSpan.FromMilliseconds(520)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            var opacity = new DoubleAnimation
            {
                From = 0.62,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(520)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(size, _entranceHighlight);
            Storyboard.SetTargetProperty(size, "Width");
            Storyboard.SetTarget(opacity, _entranceHighlight);
            Storyboard.SetTargetProperty(opacity, "Opacity");
            storyboard.Children.Add(size);
            storyboard.Children.Add(opacity);
            storyboard.Begin();
        }
        catch
        {
            _entranceHighlight.Opacity = 0;
        }
    }

    private Button CreateButton(string text, string automationName, InteractionIntent intent)
    {
        var button = new Button { Content = text, MinWidth = 44, MinHeight = 32 };
        AutomationProperties.SetName(button, automationName);
        button.Click += (_, _) => InteractionRequested?.Invoke(this, new InteractionIntentRequestedEventArgs(intent));
        return button;
    }
}
