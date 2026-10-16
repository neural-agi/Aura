namespace Aura.Runtime.Context;

public interface IContextEngine
{
    ContextSnapshot Current { get; }
    event EventHandler<ContextChangedEventArgs>? ContextChanged;
    ContextSnapshot Evaluate(ContextInput input);
}

public sealed class ContextChangedEventArgs(ContextSnapshot previous, ContextSnapshot current) : EventArgs
{
    public ContextSnapshot Previous { get; } = previous;
    public ContextSnapshot Current { get; } = current;
}
