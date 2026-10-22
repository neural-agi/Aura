using Aura.UI.Interop;
using Microsoft.Extensions.Logging;
using Aura.Runtime;
using Aura.UI.Interaction;
using Aura.Runtime.Decision;
using Aura.Runtime.Notifications;
using Aura.Runtime.Artifacts;
using Aura.Runtime.Media;

namespace Aura.UI.Surface;

internal sealed class UnavailableMediaControlService : IMediaControlService
{
    public MediaProjection Current => MediaProjection.Unavailable;
    public event EventHandler<MediaProjection>? Changed { add { } remove { } }
    public ValueTask StartAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask StopAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask<MediaCommandResult> ExecuteAsync(MediaCommand command, CancellationToken cancellationToken = default) => ValueTask.FromResult(MediaCommandResult.NoSession);
    public ValueTask<bool> SetVolumeAsync(double level, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
}

/// <summary>Coordinates the visible surface projection and placement, not authoritative runtime state.</summary>
public sealed class SurfaceManager : ISurfaceManager, INotificationProjectionSink, IDisposable
{
    private const int CompactWidth = 300;
    private const int CompactHeight = 82;
    private const int ExpandedWidth = 440;
    private const int ExpandedHeight = 170;
    private const int ClipboardHistoryWidth = 520;
    private readonly ISurfaceView _view;
    private readonly IWin32Interop _interop;
    private readonly ILogger<SurfaceManager> _logger;
    private readonly IStateMachine _stateMachine;
    private readonly IInteractionEngine _interaction;
    private readonly IClipboardArtifactManager _clipboardArtifacts;
    private readonly IMediaControlService _media;

    private string? _clipboardFilter;
    private readonly object _gate = new();
    private nint _windowHandle;
    private SurfacePresentationState _current = new(SurfacePresentationMode.Hidden, false, "Aura", "Ready");
    private AuraNotification? _currentNotification;

    public SurfaceManager(ISurfaceView view, IWin32Interop interop, ILogger<SurfaceManager> logger, IStateMachine stateMachine, IInteractionEngine interaction, IClipboardArtifactManager clipboardArtifacts, IMediaControlService? media = null)
    {
        _view = view;
        _interop = interop;
        _logger = logger;
        _stateMachine = stateMachine;
        _interaction = interaction;
        _clipboardArtifacts = clipboardArtifacts;
        _media = media ?? new UnavailableMediaControlService();
        _current = _current with { Media = _media.Current };
        _stateMachine.StateChanged += OnStateChanged;
        _clipboardArtifacts.Changed += OnClipboardArtifactsChanged;
        _media.Changed += OnMediaChanged;
        _view.InteractionRequested += OnInteractionRequested;
    }

    public SurfacePresentationState Current { get { lock (_gate) return _current; } }


    public void Attach(nint windowHandle) { lock (_gate) _windowHandle = windowHandle; }

    private async void OnInteractionRequested(object? sender, InteractionIntentRequestedEventArgs e)
    {
        if (e.Intent == InteractionIntent.BrowseClipboard)
        {
            var accepted = await _interaction.RequestAsync(e.Intent, e.Value).ConfigureAwait(false);
            if (!accepted) return;
            lock (_gate)
            {
                _current = _current with
                {
                    ClipboardItems = _clipboardArtifacts.Query(_clipboardFilter),
                    ClipboardStatus = null,
                    ClipboardAvailable = true,
                    IsClipboardHistoryOpen = true,
                    Mode = SurfacePresentationMode.Interactive,
                    IsVisible = true,
                    StatusText = "Clipboard history",
                    ContextText = string.Empty
                };
            }
            ApplyPresentation(Current, _windowHandle);
            return;
        }
        if (e.Intent == InteractionIntent.FilterClipboard)
        {
            _clipboardFilter = e.Value;
            lock (_gate) _current = _current with { ClipboardItems = _clipboardArtifacts.Query(_clipboardFilter) };
            _view.RenderOnUiThread(Current);
            return;
        }
        if (e.Intent == InteractionIntent.OpenMedia)
        {
            var accepted = await _interaction.RequestAsync(InteractionIntent.EnterInteractive).ConfigureAwait(false);
            if (!accepted) return;
            lock (_gate)
            {
                _current = _current with { Media = _media.Current, IsMediaSurfaceOpen = true, IsClipboardHistoryOpen = false, Mode = SurfacePresentationMode.Interactive, IsVisible = true, StatusText = "Media controls", ContextText = string.Empty };
            }
            ApplyPresentation(Current, _windowHandle);
            return;
        }
        if (e.Intent is InteractionIntent.MediaPlayPause or InteractionIntent.MediaPrevious or InteractionIntent.MediaNext or InteractionIntent.MediaVolumeChanged)
        {
            _ = await _interaction.RequestAsync(e.Intent, e.Value).ConfigureAwait(false);
            return;
        }
        if (e.Intent == InteractionIntent.Collapse)
        {
            var accepted = await _interaction.RequestAsync(e.Intent).ConfigureAwait(false);
            if (!accepted) return;
            SurfacePresentationState state;
            nint handle;
            lock (_gate)
            {
                var isMediaOpen = _current.IsMediaSurfaceOpen;
                _current = _current with
                {
                    IsMediaSurfaceOpen = false,
                    IsClipboardHistoryOpen = false,
                    Media = isMediaOpen ? null : _current.Media,
                    Mode = isMediaOpen ? SurfacePresentationMode.Compact : _current.Mode,
                    IsVisible = true,
                    StatusText = "Ready",
                    ContextText = string.Empty
                };
                state = _current;
                handle = _windowHandle;
            }
            ApplyPresentation(state, handle);
            return;
        }
        if (e.Intent == InteractionIntent.Dismiss)
        {
            var accepted = await _interaction.RequestAsync(e.Intent).ConfigureAwait(false);
            if (!accepted) return;
            lock (_gate) _current = _current with { IsMediaSurfaceOpen = false, IsClipboardHistoryOpen = false };
            ApplyPresentation(Current, _windowHandle);
            return;
        }
        if (e.Intent is InteractionIntent.RestoreClipboard or InteractionIntent.PinClipboard
            or InteractionIntent.UnpinClipboard or InteractionIntent.DeleteClipboard)
        {
            try { _ = await _interaction.RequestAsync(e.Intent, e.Value).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
            catch (Exception) { _clipboardArtifacts.SetAvailability(false, "Clipboard history action failed."); }
            lock (_gate) _current = _current with { ClipboardItems = _clipboardArtifacts.Query(_clipboardFilter) };
            _view.RenderOnUiThread(Current);
            return;
        }
        _ = _interaction.RequestAsync(e.Intent);
    }

    private void OnMediaChanged(object? sender, MediaProjection projection)
    {
        SurfacePresentationState state;
        lock (_gate)
        {
            _current = _current with { Media = projection };
            state = _current;
        }
        _view.RenderOnUiThread(state);
    }

    private void OnClipboardArtifactsChanged(object? sender, ClipboardHistorySnapshot snapshot)
    {
        SurfacePresentationState state;
        lock (_gate)
        {
            _current = _current with
            {
                ClipboardItems = _clipboardArtifacts.Query(_clipboardFilter),
                ClipboardStatus = null,
                ClipboardAvailable = snapshot.IsAvailable
            };
            state = _current;
        }
        _view.RenderOnUiThread(state);
    }

    public void Dispose()
    {
        _stateMachine.StateChanged -= OnStateChanged;
        _view.InteractionRequested -= OnInteractionRequested;
        _clipboardArtifacts.Changed -= OnClipboardArtifactsChanged;
        _media.Changed -= OnMediaChanged;
    }

    public void Project(AuraNotification notification, RuntimeDecision decision)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(decision);
        if (!decision.IsAuthorizedForSurface)
        {
            return;
        }

        SurfacePresentationState state;
        lock (_gate)
        {
            var mode = _stateMachine.Current switch
            {
                AuraState.Surfacing => SurfacePresentationMode.Eligible,
                AuraState.Compact => SurfacePresentationMode.Compact,
                AuraState.Expanded => SurfacePresentationMode.Expanded,
                AuraState.Interactive => SurfacePresentationMode.Interactive,
                _ => (SurfacePresentationMode?)null
            };
            if (mode is null)
            {
                return;
            }

            _currentNotification = notification;
            _current = _current with { Mode = mode.Value, IsVisible = true, Notification = notification };
            state = _current;
        }

        ApplyPresentation(state, _windowHandle);
    }

    private void ApplyPresentation(SurfacePresentationState state, nint handle)
    {
        _view.RenderOnUiThread(state, () =>
        {
            if (handle != 0 && state.IsVisible) ApplyBounds(handle, state.Mode);
        });
    }

    private void ApplyBounds(nint handle, SurfacePresentationMode mode)
    {
        var monitor = _interop.GetMonitorSnapshot(handle);
        var expanded = mode is SurfacePresentationMode.Expanded or SurfacePresentationMode.Interactive;
        bool historyOpen;
        bool mediaOpen;
        lock (_gate)
        {
            historyOpen = _current.IsClipboardHistoryOpen;
            mediaOpen = _current.IsMediaSurfaceOpen;
        }
        var width = historyOpen ? ClipboardHistoryWidth : mediaOpen ? ExpandedWidth : expanded ? ExpandedWidth : CompactWidth;
        var height = historyOpen
            ? Math.Min(520, Math.Max(ExpandedHeight, monitor.WorkBottom - monitor.WorkTop - 80))
            : mediaOpen ? ExpandedHeight : expanded ? ExpandedHeight : CompactHeight;
        var left = monitor.WorkLeft + (monitor.WorkRight - monitor.WorkLeft - width) / 2;
        _interop.SetWindowBounds(handle, left, monitor.WorkTop, width, height);
    }

    private void OnStateChanged(object? sender, StateChangedEventArgs e)
    {
        var next = e.Current switch
        {
            AuraState.Surfacing => SurfacePresentationMode.Eligible,
            AuraState.Compact => SurfacePresentationMode.Compact,
            AuraState.Expanded => SurfacePresentationMode.Expanded,
            AuraState.Interactive => SurfacePresentationMode.Interactive,
            AuraState.Dismissed => SurfacePresentationMode.Dismissed,
            AuraState.Recovered => SurfacePresentationMode.Recovered,
            _ => SurfacePresentationMode.Hidden
        };
        var visible = next is not (SurfacePresentationMode.Hidden or SurfacePresentationMode.Dismissed);
        SurfacePresentationState state;
        nint handle;
        lock (_gate)
        {
            if (next is SurfacePresentationMode.Hidden or SurfacePresentationMode.Dismissed or SurfacePresentationMode.Recovered)
            {
                _currentNotification = null;
            }
            var keepClipboardHistory = _current.IsClipboardHistoryOpen
                && next is not (SurfacePresentationMode.Dismissed or SurfacePresentationMode.Hidden or SurfacePresentationMode.Recovered);
            var keepMedia = _current.IsMediaSurfaceOpen
                && next is not (SurfacePresentationMode.Dismissed or SurfacePresentationMode.Hidden or SurfacePresentationMode.Recovered);
            var statusText = keepClipboardHistory || keepMedia
                ? string.Empty
                : next switch
                {
                    SurfacePresentationMode.Expanded or SurfacePresentationMode.Interactive => "Surface actions",
                    SurfacePresentationMode.Dismissed => string.Empty,
                    SurfacePresentationMode.Recovered => "Surface recovered",
                    _ => "Ready"
                };
            _current = _current with
            {
                Mode = keepClipboardHistory || keepMedia ? SurfacePresentationMode.Interactive : next,
                IsVisible = keepClipboardHistory || keepMedia || visible,
                StatusText = keepClipboardHistory || keepMedia || next == SurfacePresentationMode.Dismissed ? string.Empty : statusText,
                ContextText = keepClipboardHistory || keepMedia ? string.Empty : next == SurfacePresentationMode.Dismissed ? string.Empty : _current.ContextText,
                Notification = _currentNotification,
                ClipboardItems = keepClipboardHistory ? _clipboardArtifacts.Query(_clipboardFilter) : _current.ClipboardItems,
                IsClipboardHistoryOpen = keepClipboardHistory,
                IsMediaSurfaceOpen = keepMedia
            };
            state = _current;
            handle = _windowHandle;
        }
        ApplyPresentation(state, handle);
        _logger.LogDebug("Surface presentation changed to {Mode}.", next);
    }
}
