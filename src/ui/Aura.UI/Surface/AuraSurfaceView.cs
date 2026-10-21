using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Input;
using Aura.Runtime.Artifacts;
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
    private readonly Button _clipboardBrowse;
    private readonly TextBox _clipboardFilter;
    private readonly StackPanel _clipboardItems;
    private readonly TextBlock _clipboardStatus;
    private readonly Grid _clipboardPanel;

    public AuraSurfaceView()
    {
        _status = new TextBlock { Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), FontSize = 16 };
        _context = new TextBlock { Foreground = new SolidColorBrush(Microsoft.UI.Colors.LightGray), FontSize = 12 };
        _expand = CreateButton("Expand", "Expand Aura surface", InteractionIntent.Expand);
        _collapse = CreateButton("Collapse", "Collapse Aura surface", InteractionIntent.Collapse);
        _interactive = CreateButton("Open actions", "Open Aura surface actions", InteractionIntent.EnterInteractive);
        _dismiss = CreateButton("Dismiss", "Dismiss Aura surface", InteractionIntent.Dismiss);
        _recover = CreateButton("Recover", "Recover Aura surface", InteractionIntent.Recover);
        _clipboardBrowse = CreateButton("Clipboard history", "Browse clipboard history", InteractionIntent.BrowseClipboard);
        _clipboardFilter = new TextBox { PlaceholderText = "Filter clipboard history" };
        AutomationProperties.SetName(_clipboardFilter, "Filter clipboard history");
        _clipboardFilter.TextChanged += (_, _) => InteractionRequested?.Invoke(this, new(InteractionIntent.FilterClipboard, _clipboardFilter.Text));
        _clipboardItems = new StackPanel { Spacing = 2 };
        _clipboardStatus = new TextBlock { Foreground = new SolidColorBrush(Microsoft.UI.Colors.LightGray), FontSize = 12, Margin = new Thickness(4, 0, 4, 0) };
        var clipboardHeader = new TextBlock { Text = "Clipboard history", Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), FontSize = 16, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(clipboardHeader, "Clipboard history");
        _clipboardFilter.MinHeight = 32;
        var clipboardScroll = new ScrollViewer
        {
            Content = _clipboardItems,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalAlignment = VerticalAlignment.Stretch,
            Padding = new Thickness(0, 0, 2, 0),
            IsTabStop = false
        };
        var historyHeaderRow = new Grid { ColumnSpacing = 8 };
        historyHeaderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        historyHeaderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var historyDismiss = CreateButton("Dismiss", "Dismiss Aura surface", InteractionIntent.Dismiss);
        historyDismiss.MinWidth = 56;
        historyDismiss.MaxWidth = 56;
        historyDismiss.MinHeight = 28;
        historyDismiss.FontSize = 12;
        historyHeaderRow.Children.Add(clipboardHeader);
        var historyActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        _collapse.MinWidth = 64;
        _collapse.MaxWidth = 64;
        _collapse.MinHeight = 28;
        _collapse.FontSize = 12;
        historyActions.Children.Add(_collapse);
        historyActions.Children.Add(historyDismiss);
        Grid.SetColumn(historyActions, 1);
        historyHeaderRow.Children.Add(historyActions);
        _clipboardPanel = new Grid { RowSpacing = 6, Padding = new Thickness(18, 10, 18, 10), Visibility = Visibility.Collapsed };
        _clipboardPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _clipboardPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _clipboardPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _clipboardPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _clipboardPanel.Children.Add(historyHeaderRow);
        Grid.SetRow(_clipboardFilter, 1);
        _clipboardPanel.Children.Add(_clipboardFilter);
        Grid.SetRow(_clipboardStatus, 2);
        _clipboardPanel.Children.Add(_clipboardStatus);
        Grid.SetRow(clipboardScroll, 3);
        _clipboardPanel.Children.Add(clipboardScroll);

        var content = new Grid { Padding = new Thickness(18, 12, 18, 12) };
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        actions.Children.Add(_expand);
        actions.Children.Add(_interactive);
        actions.Children.Add(_dismiss);
        actions.Children.Add(_recover);
        actions.Children.Add(_clipboardBrowse);
        Grid.SetRow(_status, 0);
        Grid.SetRow(_context, 1);
        Grid.SetRow(actions, 2);
        Grid.SetRow(_clipboardPanel, 3);
        content.Children.Add(_status);
        content.Children.Add(_context);
        content.Children.Add(actions);
        content.Children.Add(_clipboardPanel);

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
        _status.Text = state.Notification?.Title ?? (state.Mode == SurfacePresentationMode.Dismissed ? string.Empty : state.StatusText);
        _context.Text = state.Notification is { } notification
            ? $"{notification.SourceApp}: {notification.Body}"
            : state.ContextText;

        Root.Visibility = state.IsVisible || state.Mode == SurfacePresentationMode.Dismissed
            ? Visibility.Visible
            : Visibility.Collapsed;
        var dismissed = state.Mode == SurfacePresentationMode.Dismissed;
        var historyOpen = state.IsClipboardHistoryOpen;
        _status.Visibility = historyOpen ? Visibility.Collapsed : Visibility.Visible;
        _context.Visibility = historyOpen ? Visibility.Collapsed : Visibility.Visible;
        _expand.Visibility = !historyOpen && (state.Mode is SurfacePresentationMode.Compact or SurfacePresentationMode.Eligible) ? Visibility.Visible : Visibility.Collapsed;
        _interactive.Visibility = !historyOpen && state.Mode == SurfacePresentationMode.Expanded ? Visibility.Visible : Visibility.Collapsed;
        _dismiss.Visibility = state.IsVisible && !dismissed && !historyOpen ? Visibility.Visible : Visibility.Collapsed;
        _recover.Visibility = dismissed || state.Mode == SurfacePresentationMode.Hidden ? Visibility.Visible : Visibility.Collapsed;
        _collapse.Visibility = historyOpen || (state.Mode is SurfacePresentationMode.Expanded or SurfacePresentationMode.Interactive) ? Visibility.Visible : Visibility.Collapsed;
        _clipboardBrowse.Visibility = !historyOpen && !dismissed ? Visibility.Visible : Visibility.Collapsed;
        _clipboardPanel.Visibility = historyOpen ? Visibility.Visible : Visibility.Collapsed;
        _clipboardStatus.Text = historyOpen
            ? state.ClipboardItems is { Count: > 0 }
                ? string.Empty
                : state.ClipboardAvailable ? "No clipboard items yet." : "Clipboard history is unavailable."
            : string.Empty;
        _clipboardItems.Children.Clear();
        foreach (var item in state.ClipboardItems ?? Array.Empty<ClipboardArtifact>())
        {
            var row = new Grid { ColumnSpacing = 6, MinHeight = 32, Margin = new Thickness(0, 1, 2, 1) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            var preview = item.Text.Replace("\\r", " ", StringComparison.Ordinal).Replace("\\n", " ", StringComparison.Ordinal).Trim();
            var select = new Button
            {
                Content = new TextBlock { Text = preview, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap },
                MinHeight = 30,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(10, 3, 8, 3),
                Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(38, 220, 235, 250)),
                BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(40, 220, 235, 250)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10)
            };
            AutomationProperties.SetName(select, $"Restore clipboard item: {preview}");
            select.Click += (_, _) => InteractionRequested?.Invoke(this, new(InteractionIntent.RestoreClipboard, item.Id));
            var pin = CreateButton(item.IsPinned ? "Pinned" : "Pin", item.IsPinned ? "Unpin clipboard item" : "Pin clipboard item",
                item.IsPinned ? InteractionIntent.UnpinClipboard : InteractionIntent.PinClipboard, item.Id);
            var delete = CreateButton("×", "Delete clipboard item", InteractionIntent.DeleteClipboard, item.Id);
            pin.MinWidth = 64;
            pin.MaxWidth = 64;
            pin.MinHeight = 30;
            delete.MinWidth = 36;
            delete.MaxWidth = 36;
            delete.MinHeight = 30;
            delete.FontSize = 16;
            Grid.SetColumn(pin, 1);
            Grid.SetColumn(delete, 2);
            row.Children.Add(select);
            row.Children.Add(pin);
            row.Children.Add(delete);
            _clipboardItems.Children.Add(row);
        }
    }

    public void RenderOnUiThread(SurfacePresentationState state, Action? afterRender = null)
    {
        void Apply()
        {
            Render(state);
            afterRender?.Invoke();
        }

        if (Root.DispatcherQueue.HasThreadAccess) Apply();
        else _ = Root.DispatcherQueue.TryEnqueue(Apply);
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

    private Button CreateButton(string text, string automationName, InteractionIntent intent, string? value = null)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 44,
            MinHeight = 30,
            Padding = new Thickness(8, 3, 8, 3),
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(34, 220, 235, 250)),
            BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(48, 220, 235, 250)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10)
        };
        AutomationProperties.SetName(button, automationName);
        button.Click += (_, _) => InteractionRequested?.Invoke(this, new InteractionIntentRequestedEventArgs(intent, value));
        return button;
    }
}
