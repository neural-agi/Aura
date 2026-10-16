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
        var unavailable = input.UnavailableSignals ?? new HashSet<string>(StringComparer.Ordinal);
        // PROVISIONAL: category evidence weights and precedence are not numerically specified by the canonical context model.
        AddApplicationEvidence(evidence, input.ActiveApplication, unavailable);
        Add(evidence, input.IsMeeting, "meeting", ContextCategory.Meeting, unavailable);
        Add(evidence, input.IsGaming, "gaming", ContextCategory.Gaming, unavailable);
        Add(evidence, input.IsPresentation, "presentation", ContextCategory.Presentation, unavailable);
        Add(evidence, input.IsFocusSession, "focus-session", ContextCategory.FocusSession, unavailable);
        Add(evidence, input.IsOffline, "offline", ContextCategory.Offline, unavailable);
        Add(evidence, input.IsBatterySaver, "battery-saver", ContextCategory.BatterySaver, unavailable);

        if (input.AdditionalEvidence is not null)
        {
            foreach (var category in input.AdditionalEvidence.Order())
            {
                if (!Enum.IsDefined(category) || category == ContextCategory.Unknown)
                {
                    throw new ArgumentOutOfRangeException(nameof(input), category, "Additional context evidence must name a known non-unknown category.");
                }

                evidence.Add(new ContextEvidence($"declared:{category}", category, 2));
            }
        }

        var availableEvidence = evidence.Where(item => item.IsAvailable).ToArray();
        // PROVISIONAL: equal evidence totals are resolved by enum order for deterministic results.
        var selected = availableEvidence.Length == 0
            ? ContextCategory.Unknown
            : availableEvidence.GroupBy(item => item.Category)
                .OrderByDescending(group => group.Sum(item => item.Weight))
                .ThenBy(group => group.Key)
                .First().Key;
        var total = availableEvidence.Sum(item => item.Weight);
        var confidence = total == 0
            ? 0
            : Math.Clamp(availableEvidence.Where(item => item.Category == selected).Sum(item => item.Weight) / total, 0, 1);
        var snapshot = new ContextSnapshot(selected, confidence, evidence, availableEvidence.Length > 0 || input.IsUserActive);

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

    // PROVISIONAL: app-name substring mappings are provider-agnostic heuristics, not canonical mappings.
    private static void AddApplicationEvidence(List<ContextEvidence> evidence, string? application, IReadOnlySet<string> unavailable)
    {
        const string signal = "active-application";
        if (unavailable.Contains(signal))
        {
            evidence.Add(new ContextEvidence(signal, ContextCategory.Unknown, 0, IsAvailable: false));
            return;
        }

        if (string.IsNullOrWhiteSpace(application)) return;

        var normalized = application.Trim();
        var category = normalized.Contains("devenv", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("code", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("terminal", StringComparison.OrdinalIgnoreCase)
            ? ContextCategory.Coding
            : normalized.Contains("browser", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("chrome", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("firefox", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("edge", StringComparison.OrdinalIgnoreCase)
                    ? ContextCategory.Browsing
                    : (ContextCategory?)null;

        if (category is not null)
        {
            evidence.Add(new ContextEvidence(signal, category.Value, 1));
        }
    }

    private static void Add(List<ContextEvidence> evidence, bool active, string signal, ContextCategory category, IReadOnlySet<string> unavailable)
    {
        if (unavailable.Contains(signal))
        {
            evidence.Add(new ContextEvidence(signal, category, 0, IsAvailable: false));
        }
        else if (active)
        {
            evidence.Add(new ContextEvidence(signal, category, 2));
        }
    }

    private static bool Equivalent(ContextSnapshot left, ContextSnapshot right)
        => left.Category == right.Category
            && left.Confidence == right.Confidence
            && left.IsAvailable == right.IsAvailable
            && left.Evidence.SequenceEqual(right.Evidence);
}
