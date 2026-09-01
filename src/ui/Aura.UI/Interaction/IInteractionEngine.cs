namespace Aura.UI.Interaction;

public interface IInteractionEngine
{
    bool Request(InteractionIntent intent);
}
