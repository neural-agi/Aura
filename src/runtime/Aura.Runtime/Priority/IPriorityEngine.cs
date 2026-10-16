namespace Aura.Runtime.Priority;

public interface IPriorityEngine
{
    PriorityDecision Evaluate(PriorityInput input);
}
