using Microsoft.UI.Xaml;
using Aura.UI.Interaction;

namespace Aura.UI.Surface;

public interface ISurfaceView
{
    UIElement Root { get; }
    event EventHandler<InteractionIntentRequestedEventArgs> InteractionRequested;
    void Render(SurfacePresentationState state);
    void RenderOnUiThread(SurfacePresentationState state, Action? afterRender = null);
    void BeginStartupEntrance();
}

public sealed class InteractionIntentRequestedEventArgs(InteractionIntent intent, string? value = null) : EventArgs
{
    public InteractionIntent Intent { get; } = intent;
    public string? Value { get; } = value;
}
