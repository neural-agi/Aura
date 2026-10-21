namespace Aura.UI.Interaction;

public interface IInteractionEngine
{
    ValueTask<bool> RequestAsync(InteractionIntent intent, string? value = null, CancellationToken cancellationToken = default);
}
