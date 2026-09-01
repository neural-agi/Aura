namespace Aura.Runtime;

/// <summary>Authoritative Aura state owner. UI state is only a projection of this state.</summary>
public sealed class StateMachine : IStateMachine
{
    private static readonly IReadOnlyDictionary<AuraState, AuraState[]> Allowed =
        new Dictionary<AuraState, AuraState[]>
        {
            [AuraState.Idle] = [AuraState.Surfacing],
            [AuraState.Surfacing] = [AuraState.Compact, AuraState.Dismissed],
            [AuraState.Compact] = [AuraState.Expanded, AuraState.Interactive, AuraState.Dismissed, AuraState.Idle],
            [AuraState.Expanded] = [AuraState.Compact, AuraState.Interactive, AuraState.Dismissed],
            [AuraState.Interactive] = [AuraState.Compact, AuraState.Dismissed],
            [AuraState.Dismissed] = [AuraState.Recovered, AuraState.Idle],
            [AuraState.Recovered] = [AuraState.Compact]
        };

    private readonly object _gate = new();
    private AuraState _current = AuraState.Idle;

    public AuraState Current { get { lock (_gate) return _current; } }
    public event EventHandler<StateChangedEventArgs>? StateChanged;

    public bool TryTransition(AuraState next)
    {
        StateChangedEventArgs? change = null;
        lock (_gate)
        {
            if (_current == next || !Allowed[_current].Contains(next))
            {
                return false;
            }

            var previous = _current;
            _current = next;
            change = new StateChangedEventArgs(previous, next);
        }

        StateChanged?.Invoke(this, change);
        return true;
    }
}
