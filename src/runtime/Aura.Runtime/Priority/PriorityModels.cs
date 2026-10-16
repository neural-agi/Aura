using Aura.Runtime.Context;

namespace Aura.Runtime.Priority;

public enum PriorityClass { Low, Medium, High, Critical }
public enum PriorityOutcome { Surface, Silent, Queue, Merge, Suppress, Expire }

public sealed record PriorityInput(
    string Id,
    PriorityClass BasePriority = PriorityClass.Low,
    bool IsSystemCritical = false,
    bool IsPrioritySource = false,
    bool IsTimeSensitive = false,
    bool IsContextRelevant = false,
    bool IsActionable = false,
    bool IsNovel = true,
    bool IsExpired = false,
    bool IsRepeated = false,
    bool CanMerge = false,
    bool PolicyAllowed = true,
    ContextSnapshot? Context = null);

public sealed record PriorityDecision(
    PriorityClass Classification,
    PriorityOutcome Outcome,
    bool ShouldSurface,
    bool IsCritical,
    string Reason,
    ContextCategory Context,
    bool PolicyAllowed);
