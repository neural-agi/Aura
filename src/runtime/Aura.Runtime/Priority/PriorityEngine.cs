using Aura.Runtime.Context;

namespace Aura.Runtime.Priority;

public sealed class PriorityEngine : IPriorityEngine
{
    public PriorityDecision Evaluate(PriorityInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var context = input.Context?.Category ?? ContextCategory.Unknown;
        if (!input.PolicyAllowed) return New(PriorityClass.Low, PriorityOutcome.Suppress, false, false, "Denied by policy", context, false);
        if (input.IsExpired) return New(PriorityClass.Low, PriorityOutcome.Expire, false, false, "Expired", context, true);
        if (input.IsSystemCritical) return New(PriorityClass.Critical, PriorityOutcome.Surface, true, true, "System criticality", context, true);
        var classification = input.BasePriority;
        if (input.IsPrioritySource && classification < PriorityClass.High) classification = PriorityClass.High;
        if (input.IsTimeSensitive && classification < PriorityClass.High) classification = PriorityClass.High;
        var outcome = input.IsRepeated && input.CanMerge ? PriorityOutcome.Merge : classification switch
        {
            PriorityClass.High or PriorityClass.Critical => PriorityOutcome.Surface,
            PriorityClass.Medium when input.IsContextRelevant || input.IsActionable => PriorityOutcome.Surface,
            PriorityClass.Medium => PriorityOutcome.Queue,
            _ => PriorityOutcome.Silent
        };
        var surface = outcome is PriorityOutcome.Surface or PriorityOutcome.Merge;
        return New(classification, outcome, surface, classification == PriorityClass.Critical, Explain(input, classification, outcome), context, true);
    }

    private static string Explain(PriorityInput input, PriorityClass classification, PriorityOutcome outcome)
        => $"{classification}; outcome={outcome}; critical={input.IsSystemCritical}; priority-source={input.IsPrioritySource}; time-sensitive={input.IsTimeSensitive}; context-relevant={input.IsContextRelevant}; actionable={input.IsActionable}; repeated={input.IsRepeated}";

    private static PriorityDecision New(PriorityClass c, PriorityOutcome o, bool surface, bool critical, string reason, ContextCategory context, bool allowed)
        => new(c, o, surface, critical, reason, context, allowed);
}
