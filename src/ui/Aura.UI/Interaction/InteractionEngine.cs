using Aura.Runtime;

namespace Aura.UI.Interaction;

/// <summary>Routes deliberate user intent to the authoritative state machine.</summary>
public sealed class InteractionEngine(IStateMachine stateMachine) : IInteractionEngine
{
    public bool Request(InteractionIntent intent)
    {
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
