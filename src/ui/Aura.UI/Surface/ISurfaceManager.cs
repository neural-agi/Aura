namespace Aura.UI.Surface;

public interface ISurfaceManager
{
    SurfacePresentationState Current { get; }
    void Attach(nint windowHandle);
}
