namespace Aura.Runtime.Context;

public sealed record ContextEvidence(string Signal, ContextCategory Category, double Weight, bool IsAvailable = true);

public sealed record ContextSnapshot(
    ContextCategory Category,
    double Confidence,
    IReadOnlyList<ContextEvidence> Evidence,
    bool IsAvailable)
{
    public static ContextSnapshot Unknown => new(ContextCategory.Unknown, 0, [], false);
}

public sealed record ContextInput(
    string? ActiveApplication = null,
    bool IsMeeting = false,
    bool IsGaming = false,
    bool IsPresentation = false,
    bool IsFocusSession = false,
    bool IsOffline = false,
    bool IsBatterySaver = false,
    bool IsUserActive = true,
    IReadOnlySet<ContextCategory>? AdditionalEvidence = null,
    IReadOnlySet<string>? UnavailableSignals = null);
