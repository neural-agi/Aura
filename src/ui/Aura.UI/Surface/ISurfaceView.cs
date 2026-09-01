using Microsoft.UI.Xaml;
using Aura.UI.Interaction;

namespace Aura.UI.Surface;

public interface ISurfaceView
{
    UIElement Root { get; }
    event EventHandler<InteractionIntentRequestedEventArgs> InteractionRequested;
    void Render(SurfacePresentationState state);
}

public sealed class InteractionIntentRequestedEventArgs(InteractionIntent intent) : EventArgs
{
    public InteractionIntent Intent { get; } = intent;
}
