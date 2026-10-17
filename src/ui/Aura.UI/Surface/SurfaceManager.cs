using Aura.UI.Interop;
using Microsoft.Extensions.Logging;
using Aura.Runtime;
using Aura.UI.Interaction;
using Aura.Runtime.Decision;
using Aura.Runtime.Notifications;

namespace Aura.UI.Surface;

/// <summary>Coordinates the visible surface projection and placement, not authoritative runtime state.</summary>
public sealed class SurfaceManager : ISurfaceManager, INotificationProjectionSink
{
    private const int CompactWidth = 300;
    private const int CompactHeight = 82;
    private const int ExpandedWidth = 440;
    private const int ExpandedHeight = 170;
    private readonly ISurfaceView _view;
    private readonly IWin32Interop _interop;
    private readonly ILogger<SurfaceManager> _logger;
    private readonly IStateMachine _stateMachine;
    private readonly IInteractionEngine _interaction;
    private readonly object _gate = new();
    private nint _windowHandle;
    private SurfacePresentationState _current = new(SurfacePresentationMode.Hidden, false, "Aura", "Ready");
    private AuraNotification? _currentNotification;

    public SurfaceManager(ISurfaceView view, IWin32Interop interop, ILogger<SurfaceManager> logger, IStateMachine stateMachine, IInteractionEngine interaction)
    {
        _view = view;
        _interop = interop;
        _logger = logger;
        _stateMachine = stateMachine;
        _interaction = interaction;
        _stateMachine.StateChanged += OnStateChanged;
        _view.InteractionRequested += OnInteractionRequested;
    }

    public SurfacePresentationState Current { get { lock (_gate) return _current; } }
    public void Attach(nint windowHandle) { lock (_gate) _windowHandle = windowHandle; }

    private void OnInteractionRequested(object? sender, InteractionIntentRequestedEventArgs e) => _interaction.Request(e.Intent);

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

        _view.Render(state);
        if (_windowHandle != 0)
        {
            var mode = state.Mode;
            var width = mode is SurfacePresentationMode.Expanded or SurfacePresentationMode.Interactive ? ExpandedWidth : CompactWidth;
            var height = mode is SurfacePresentationMode.Expanded or SurfacePresentationMode.Interactive ? ExpandedHeight : CompactHeight;
            var monitor = _interop.GetMonitorSnapshot(_windowHandle);
            var left = monitor.WorkLeft + (monitor.WorkRight - monitor.WorkLeft - width) / 2;
            _interop.SetWindowBounds(_windowHandle, left, monitor.WorkTop, width, height);
        }
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
            _current = new SurfacePresentationState(next, visible, "Aura", next switch
            {
                SurfacePresentationMode.Expanded or SurfacePresentationMode.Interactive => "Surface actions",
                SurfacePresentationMode.Dismissed => "Surface dismissed",
                SurfacePresentationMode.Recovered => "Surface recovered",
                _ => "Ready"
            }, _currentNotification);
            state = _current;
            handle = _windowHandle;
        }
        _view.Render(state);
        if (handle != 0 && visible)
        {
            var monitor = _interop.GetMonitorSnapshot(handle);
            var width = next is SurfacePresentationMode.Expanded or SurfacePresentationMode.Interactive ? ExpandedWidth : CompactWidth;
            var height = next is SurfacePresentationMode.Expanded or SurfacePresentationMode.Interactive ? ExpandedHeight : CompactHeight;
            var left = monitor.WorkLeft + (monitor.WorkRight - monitor.WorkLeft - width) / 2;
            _interop.SetWindowBounds(handle, left, monitor.WorkTop, width, height);
        }
        _logger.LogDebug("Surface presentation changed to {Mode}.", next);
    }
}
