using Aura.Runtime;
using Aura.Runtime.Artifacts;

namespace Aura.UI.Interaction;

/// <summary>Routes deliberate user intent to the authoritative state machine.</summary>
public sealed class InteractionEngine(IStateMachine stateMachine, IClipboardArtifactManager clipboard) : IInteractionEngine
{
    public async ValueTask<bool> RequestAsync(InteractionIntent intent, string? value = null, CancellationToken cancellationToken = default)
    {
        if (intent == InteractionIntent.BrowseClipboard)
            return stateMachine.Current is AuraState.Interactive or AuraState.Expanded
                || stateMachine.TryTransition(AuraState.Interactive);
        if (intent == InteractionIntent.FilterClipboard) return true;
        if (intent is InteractionIntent.RestoreClipboard or InteractionIntent.PinClipboard
            or InteractionIntent.UnpinClipboard or InteractionIntent.DeleteClipboard)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            return intent switch
            {
                InteractionIntent.RestoreClipboard => await clipboard.RestoreAsync(value, cancellationToken).ConfigureAwait(false),
                InteractionIntent.PinClipboard => await clipboard.SetPinnedAsync(value, true, cancellationToken).ConfigureAwait(false),
                InteractionIntent.UnpinClipboard => await clipboard.SetPinnedAsync(value, false, cancellationToken).ConfigureAwait(false),
                InteractionIntent.DeleteClipboard => await clipboard.DeleteAsync(value, cancellationToken).ConfigureAwait(false),
                _ => false
            };
        }

        var target = intent switch
        {
            InteractionIntent.Expand => AuraState.Expanded,
            InteractionIntent.Collapse => AuraState.Compact,
            InteractionIntent.EnterInteractive => AuraState.Interactive,
            InteractionIntent.Dismiss => AuraState.Dismissed,
            InteractionIntent.Recover => AuraState.Recovered,
            _ => throw new ArgumentOutOfRangeException(nameof(intent))
        };

        if (intent == InteractionIntent.Recover)
        {
            return stateMachine.TryTransition(AuraState.Recovered)
                && stateMachine.TryTransition(AuraState.Compact);
        }

        return stateMachine.TryTransition(target);
    }
}
