namespace Aura.UI.Surface;

/// <summary>Visible surface projection; this is not authoritative runtime state.</summary>
public sealed record SurfacePresentationState(
    SurfacePresentationMode Mode,
    bool IsVisible,
    string StatusText,
    string ContextText);

public enum SurfacePresentationMode
{
    Hidden, Eligible, Compact, Expanded, Interactive, Dismissed, Recovered
}
