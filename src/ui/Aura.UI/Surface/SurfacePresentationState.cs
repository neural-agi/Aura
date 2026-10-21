using Aura.Runtime.Notifications;
using Aura.Runtime.Artifacts;

namespace Aura.UI.Surface;

/// <summary>Visible surface projection; this is not authoritative runtime state.</summary>
public sealed record SurfacePresentationState(
    SurfacePresentationMode Mode,
    bool IsVisible,
    string StatusText,
    string ContextText,
    AuraNotification? Notification = null,
    IReadOnlyList<ClipboardArtifact>? ClipboardItems = null,
    string? ClipboardStatus = null,
    bool ClipboardAvailable = true,
    bool IsClipboardHistoryOpen = false);

public enum SurfacePresentationMode
{
    Hidden, Eligible, Compact, Expanded, Interactive, Dismissed, Recovered
}
