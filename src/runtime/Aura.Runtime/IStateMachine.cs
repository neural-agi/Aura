namespace Aura.Runtime;

public interface IStateMachine
{
    AuraState Current { get; }
    event EventHandler<StateChangedEventArgs>? StateChanged;
    bool TryTransition(AuraState next);
}

public sealed class StateChangedEventArgs(AuraState previous, AuraState current) : EventArgs
{
    public AuraState Previous { get; } = previous;
    public AuraState Current { get; } = current;
}
