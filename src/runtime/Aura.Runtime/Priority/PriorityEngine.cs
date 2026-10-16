using Aura.Runtime.Context;

namespace Aura.Runtime.Priority;

public sealed class PriorityEngine : IPriorityEngine
{
    public PriorityDecision Evaluate(PriorityInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (string.IsNullOrWhiteSpace(input.Id))
        {
            throw new ArgumentException("Priority input must have a non-empty identity.", nameof(input));
        }

        if (!Enum.IsDefined(input.BasePriority))
        {
            throw new ArgumentOutOfRangeException(nameof(input), input.BasePriority, "Priority class is invalid.");
        }

        var context = input.Context?.Category ?? ContextCategory.Unknown;
        if (!Enum.IsDefined(context))
        {
            throw new ArgumentOutOfRangeException(nameof(input), context, "Context category is invalid.");
        }

        var now = input.EvaluatedAtUtc ?? DateTimeOffset.UtcNow;
        var stale = input.IsExpired || IsStale(input, now);
        if (stale) return New(PriorityClass.Low, PriorityOutcome.Expire, false, false, "Expired or stale", context, input.PolicyAllowed);
        if (!input.PolicyAllowed) return New(PriorityClass.Low, PriorityOutcome.Suppress, false, false, "Denied by policy", context, false);

        // PROVISIONAL: this gate order is deterministic but the exact precedence is not specified by canonical M3 docs.
        var contextRelevant = IsContextRelevant(input);
        var classification = input.IsSystemCritical
            ? PriorityClass.Critical
            : input.IsUserDeclaredPriority || input.IsPrioritySource
                ? Max(input.BasePriority, PriorityClass.High)
                : input.IsTimeSensitive
                    ? Max(input.BasePriority, PriorityClass.High)
                    : contextRelevant
                        ? Max(input.BasePriority, PriorityClass.Medium)
                        : input.IsActionable
                            ? Max(input.BasePriority, PriorityClass.Medium)
                            : input.BasePriority;

        if (!input.IsSystemCritical && !input.IsUserDeclaredPriority && !input.IsPrioritySource && !input.IsTimeSensitive
            && !contextRelevant && !input.IsActionable && !input.IsNovel)
        {
            classification = Min(classification, PriorityClass.Low);
        }

        var outcome = input.IsRepeated && input.CanMerge
            ? PriorityOutcome.Merge
            : IsRestrictiveContext(context) && !input.IsSystemCritical
                ? PriorityOutcome.Queue
                : classification switch
                {
                    PriorityClass.Critical or PriorityClass.High => PriorityOutcome.Surface,
                    PriorityClass.Medium when input.IsActionable || contextRelevant => PriorityOutcome.Surface,
                    PriorityClass.Medium => PriorityOutcome.Queue,
                    _ when !input.IsNovel => PriorityOutcome.Queue,
                    _ => PriorityOutcome.Silent
                };

        return New(classification, outcome, outcome is PriorityOutcome.Surface or PriorityOutcome.Merge,
            classification == PriorityClass.Critical,
            Explain(input, classification, outcome, contextRelevant), context, true);
    }

    // PROVISIONAL: expiration requires a caller-supplied maximum age; strict greater-than determines staleness.
    private static bool IsStale(PriorityInput input, DateTimeOffset now)
        => input.OccurredAtUtc is { } occurredAt
            && input.MaximumAge is { } maximumAge
            && maximumAge >= TimeSpan.Zero
            && now - occurredAt > maximumAge;

    // PROVISIONAL: context-to-event-ID taxonomy below is implementation-only and not canonical product mapping.
    private static bool IsContextRelevant(PriorityInput input)
    {
        if (input.IsContextRelevant) return true;
        if (input.Context is not { IsAvailable: true } context) return false;
        return context.Category switch
        {
            ContextCategory.Meeting => input.Id.Contains("meeting", StringComparison.OrdinalIgnoreCase),
            ContextCategory.Gaming or ContextCategory.Presentation or ContextCategory.FocusSession => input.IsSystemCritical,
            ContextCategory.BatterySaver => input.Id.Contains("power", StringComparison.OrdinalIgnoreCase)
                || input.Id.Contains("battery", StringComparison.OrdinalIgnoreCase),
            ContextCategory.Offline => input.Id.Contains("offline", StringComparison.OrdinalIgnoreCase)
                || input.Id.Contains("local", StringComparison.OrdinalIgnoreCase),
            ContextCategory.Coding => input.Id.Contains("code", StringComparison.OrdinalIgnoreCase)
                || input.Id.Contains("clipboard", StringComparison.OrdinalIgnoreCase)
                || input.Id.Contains("search", StringComparison.OrdinalIgnoreCase),
            ContextCategory.CreativeWork => input.Id.Contains("file", StringComparison.OrdinalIgnoreCase)
                || input.Id.Contains("media", StringComparison.OrdinalIgnoreCase),
            ContextCategory.Browsing => input.Id.Contains("search", StringComparison.OrdinalIgnoreCase)
                || input.Id.Contains("browser", StringComparison.OrdinalIgnoreCase),
            ContextCategory.Studying => input.Id.Contains("study", StringComparison.OrdinalIgnoreCase)
                || input.Id.Contains("clipboard", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    // PROVISIONAL: Queue is the chosen noncritical outcome for these contexts; canonical docs do not mandate this exact outcome.
    private static bool IsRestrictiveContext(ContextCategory context)
        => context is ContextCategory.Gaming or ContextCategory.Presentation or ContextCategory.FocusSession;

    private static PriorityClass Max(PriorityClass left, PriorityClass right) => left > right ? left : right;
    private static PriorityClass Min(PriorityClass left, PriorityClass right) => left < right ? left : right;

    private static string Explain(PriorityInput input, PriorityClass classification, PriorityOutcome outcome, bool contextRelevant)
        => $"{(input.IsSystemCritical ? "System criticality; " : string.Empty)}{classification}; outcome={outcome}; critical={input.IsSystemCritical}; user-priority={input.IsUserDeclaredPriority}; priority-source={input.IsPrioritySource}; time-sensitive={input.IsTimeSensitive}; context-relevant={contextRelevant}; actionable={input.IsActionable}; novel={input.IsNovel}; repeated={input.IsRepeated}";

    private static PriorityDecision New(PriorityClass c, PriorityOutcome o, bool surface, bool critical, string reason, ContextCategory context, bool allowed)
        => new(c, o, surface, critical, reason, context, allowed);
}
