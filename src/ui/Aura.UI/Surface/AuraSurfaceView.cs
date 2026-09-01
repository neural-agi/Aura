using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Aura.UI.Interaction;

namespace Aura.UI.Surface;

public sealed class AuraSurfaceView : ISurfaceView
{
    private readonly Border _surface;
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

        _surface = new Border
        {
            Background = new SolidColorBrush(Microsoft.UI.Colors.DarkSlateGray),
            BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Gray),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(24),
            Child = content,
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

    private Button CreateButton(string text, string automationName, InteractionIntent intent)
    {
        var button = new Button { Content = text, MinWidth = 44, MinHeight = 32 };
        AutomationProperties.SetName(button, automationName);
        button.Click += (_, _) => InteractionRequested?.Invoke(this, new InteractionIntentRequestedEventArgs(intent));
        return button;
    }
}
