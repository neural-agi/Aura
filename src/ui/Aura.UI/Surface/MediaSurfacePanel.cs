using Aura.Runtime.Media;
using Aura.UI.Interaction;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.UI;

namespace Aura.UI.Surface;

internal sealed class MediaSurfacePanel
{
    private readonly Grid _root;
    private readonly Image _artwork;
    private readonly TextBlock _fallback;
    private readonly TextBlock _title;
    private readonly TextBlock _secondary;
    private readonly TextBlock _source;
    private readonly Button _previous;
    private readonly Button _playPause;
    private readonly Button _next;
    private readonly Slider _volume;
    private readonly TextBlock _volumeValue;
    private readonly Button _home;
    private bool _rendering;

    public MediaSurfacePanel(Action<InteractionIntent, string?> raiseIntent)
    {
        _root = new Grid { ColumnSpacing = 14, Padding = new Thickness(18, 2, 18, 2), Visibility = Visibility.Collapsed };
        _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(88) });
        _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var artHost = new Grid { Width = 88, Height = 88, VerticalAlignment = VerticalAlignment.Center };
        _artwork = new Image { Width = 88, Height = 88, Stretch = Stretch.UniformToFill, Visibility = Visibility.Collapsed };
        _fallback = new TextBlock { Text = "◉", FontSize = 48, Foreground = new SolidColorBrush(Microsoft.UI.Colors.LightGray), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(_fallback, "Generic media artwork");
        artHost.Children.Add(_artwork);
        artHost.Children.Add(_fallback);
        _root.Children.Add(artHost);

        var details = new Grid { RowSpacing = 3, VerticalAlignment = VerticalAlignment.Center };
        for (var row = 0; row < 5; row++) details.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _source = NewText(11, Microsoft.UI.Colors.LightGray);
        _title = NewText(17, Microsoft.UI.Colors.White);
        _title.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        _secondary = NewText(12, Microsoft.UI.Colors.LightGray);
        details.Children.Add(_source);
        Grid.SetRow(_title, 1); details.Children.Add(_title);
        Grid.SetRow(_secondary, 2); details.Children.Add(_secondary);

        var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center };
        _previous = CreateButton("|◀", "Previous track", InteractionIntent.MediaPrevious, raiseIntent, 40, 34);
        _playPause = CreateButton("▶", "Play media", InteractionIntent.MediaPlayPause, raiseIntent, 48, 38);
        _next = CreateButton("▶|", "Next track", InteractionIntent.MediaNext, raiseIntent, 40, 34);
        controls.Children.Add(_previous);
        controls.Children.Add(_playPause);
        controls.Children.Add(_next);
        Grid.SetRow(controls, 3); details.Children.Add(controls);

        var volumeRow = new Grid { ColumnSpacing = 8 };
        volumeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        volumeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        volumeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var volumeLabel = NewText(11, Microsoft.UI.Colors.LightGray);
        volumeLabel.Text = "System volume";
        AutomationProperties.SetName(volumeLabel, "System volume");
        _volume = new Slider { Minimum = 0, Maximum = 1, StepFrequency = 0.01, MinWidth = 96, MaxWidth = 180, VerticalAlignment = VerticalAlignment.Center };
        _volume.IsThumbToolTipEnabled = false;
        AutomationProperties.SetName(_volume, "System volume");
        AutomationProperties.SetHelpText(_volume, "Adjust system output volume from 0 to 100 percent.");
        _volumeValue = NewText(11, Microsoft.UI.Colors.LightGray);
        _volumeValue.MinWidth = 30;
        _volume.ValueChanged += (_, args) =>
        {
            _volumeValue.Text = MediaPresentationFormatting.FormatVolumeValue(args.NewValue);
            if (!_rendering) raiseIntent(InteractionIntent.MediaVolumeChanged, args.NewValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
        };
        _volume.PointerWheelChanged += (_, args) =>
        {
            var delta = args.GetCurrentPoint(_volume).Properties.MouseWheelDelta;
            if (delta == 0) return;

            var nextValue = Math.Clamp(_volume.Value + Math.Sign(delta) * 0.05, 0d, 1d);
            if (nextValue == _volume.Value) return;

            _volume.Value = nextValue;
            args.Handled = true;
        };
        volumeRow.Children.Add(volumeLabel);
        Grid.SetColumn(_volume, 1); volumeRow.Children.Add(_volume);
        Grid.SetColumn(_volumeValue, 2); volumeRow.Children.Add(_volumeValue);
        Grid.SetRow(volumeRow, 4); details.Children.Add(volumeRow);

        _root.Children.Add(details);
        Grid.SetColumn(details, 1);

        _home = CreateButton("‹ Home", "Return to Aura Home", InteractionIntent.Collapse, raiseIntent, 72, 30);
        _home.HorizontalAlignment = HorizontalAlignment.Right;
        _home.VerticalAlignment = VerticalAlignment.Top;
        _home.Margin = new Thickness(0, -2, 0, 0);
        _root.Children.Add(_home);
    }

    public FrameworkElement Root => _root;

    public void Render(MediaProjection projection, bool isOpen)
    {
        _root.Visibility = isOpen ? Visibility.Visible : Visibility.Collapsed;
        if (!isOpen) return;
        var session = projection.Session;
        _source.Text = session.SessionId.Length == 0 ? "Media unavailable" : MediaPresentationFormatting.FormatSourceName(session.SourceApplication);
        _title.Text = session.SessionId.Length == 0 ? "No media session" : session.Title ?? "No media information available";
        _secondary.Text = string.Join(" · ", new[] { session.Artist, session.Album }.Where(value => !string.IsNullOrWhiteSpace(value)));
        if (string.IsNullOrWhiteSpace(_secondary.Text)) _secondary.Text = " ";
        _playPause.Content = session.PlaybackState == MediaPlaybackState.Playing ? "Ⅱ" : "▶";
        AutomationProperties.SetName(_playPause, session.PlaybackState == MediaPlaybackState.Playing ? "Pause media" : "Play media");
        _playPause.IsEnabled = session.CanPlayPause;
        _previous.IsEnabled = session.CanGoPrevious;
        _next.IsEnabled = session.CanGoNext;
        _rendering = true;
        _volume.IsEnabled = projection.Volume.IsAvailable && projection.Volume.Level.HasValue;
        if (projection.Volume.Level is { } level)
        {
            var normalized = Math.Clamp(level, 0, 1);
            _volume.Value = normalized;
            _volumeValue.Text = MediaPresentationFormatting.FormatVolumeValue(normalized);

        }
        else
        {
            _volumeValue.Text = "Unavailable";
        }
        _rendering = false;
        SetArtwork(session);
    }


    private void SetArtwork(MediaSessionSnapshot session)
    {
        if (session.Artwork is not { Length: > 0 } bytes)
        {
            _artwork.Source = null;
            _artwork.Visibility = Visibility.Collapsed;
            _fallback.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            var bitmap = new BitmapImage();
            bitmap.SetSource(stream.AsRandomAccessStream());
            _artwork.Source = bitmap;
            _artwork.Visibility = Visibility.Visible;
            _fallback.Visibility = Visibility.Collapsed;
        }
        catch
        {
            _artwork.Source = null;
            _artwork.Visibility = Visibility.Collapsed;
            _fallback.Visibility = Visibility.Visible;
        }
    }

    private static TextBlock NewText(double size, Color color)
        => new() { FontSize = size, Foreground = new SolidColorBrush(color), TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap };

    private static Button CreateButton(string content, string name, InteractionIntent intent, Action<InteractionIntent, string?> raiseIntent, double width = 42, double height = 32)
    {
        var button = new Button { Content = content, MinWidth = width, MaxWidth = width, MinHeight = height, Padding = new Thickness(5, 2, 5, 2) };
        AutomationProperties.SetName(button, name);
        button.Click += (_, _) => raiseIntent(intent, null);
        return button;
    }
}
