namespace Aura.Runtime.Context;

public sealed class ContextEngine : IContextEngine
{
    private readonly object _gate = new();
    private ContextSnapshot _current = ContextSnapshot.Unknown;

    public ContextSnapshot Current { get { lock (_gate) return _current; } }
    public event EventHandler<ContextChangedEventArgs>? ContextChanged;

    public ContextSnapshot Evaluate(ContextInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var evidence = new List<ContextEvidence>();
        if (!string.IsNullOrWhiteSpace(input.ActiveApplication))
        {
            var app = input.ActiveApplication!;
            var category = app.Contains("devenv", StringComparison.OrdinalIgnoreCase) || app.Contains("code", StringComparison.OrdinalIgnoreCase)
                ? ContextCategory.Coding : ContextCategory.Browsing;
            evidence.Add(new ContextEvidence("active-application", category, 1));
        }
        Add(evidence, input.IsMeeting, "meeting", ContextCategory.Meeting);
        Add(evidence, input.IsGaming, "gaming", ContextCategory.Gaming);
        Add(evidence, input.IsPresentation, "presentation", ContextCategory.Presentation);
        Add(evidence, input.IsFocusSession, "focus-session", ContextCategory.FocusSession);
        Add(evidence, input.IsOffline, "offline", ContextCategory.Offline);
        Add(evidence, input.IsBatterySaver, "battery-saver", ContextCategory.BatterySaver);

        var selected = evidence.Count == 0
            ? ContextCategory.Unknown
            : evidence.GroupBy(e => e.Category).OrderByDescending(g => g.Sum(e => e.Weight)).ThenBy(g => g.Key).First().Key;
        var total = evidence.Sum(e => e.Weight);
        var confidence = evidence.Count == 0 ? 0 : Math.Clamp(evidence.Where(e => e.Category == selected).Sum(e => e.Weight) / total, 0, 1);
        var snapshot = new ContextSnapshot(selected, confidence, evidence, evidence.Count > 0 || input.IsUserActive);

        ContextChangedEventArgs? change = null;
        lock (_gate)
        {
            if (Equivalent(_current, snapshot)) return _current;
            change = new ContextChangedEventArgs(_current, snapshot);
            _current = snapshot;
        }
        ContextChanged?.Invoke(this, change);
        return snapshot;
    }

    private static void Add(List<ContextEvidence> evidence, bool active, string signal, ContextCategory category)
    {
        if (active) evidence.Add(new ContextEvidence(signal, category, 2));
    }

    private static bool Equivalent(ContextSnapshot left, ContextSnapshot right)
        => left.Category == right.Category
            && left.Confidence == right.Confidence
            && left.IsAvailable == right.IsAvailable
            && left.Evidence.SequenceEqual(right.Evidence);
}
