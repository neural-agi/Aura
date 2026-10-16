using Aura.Runtime.Context;
using Aura.Runtime.Policy;
using Aura.Runtime.Priority;
using Aura.Runtime.Settings;
using Aura.Runtime.Decision;
using Aura.Runtime;
using FluentAssertions;

namespace Aura.Tests.Unit;

public sealed class M3RuntimeTests
{
    [Xunit.Fact]
    public void ContextEvaluationIsDeterministicAndDoesNotRepublishIdenticalSnapshots()
    {
        var engine = new ContextEngine();
        var changes = 0;
        engine.ContextChanged += (_, _) => changes++;
        var input = new ContextInput(ActiveApplication: "devenv.exe", IsFocusSession: true);

        var first = engine.Evaluate(input);
        var second = engine.Evaluate(input);

        first.Should().Be(second);
        first.Category.Should().Be(ContextCategory.FocusSession);
        first.Confidence.Should().BeApproximately(2d / 3d, 0.0001);
        changes.Should().Be(1);
    }

    [Xunit.Theory]
    [Xunit.InlineData(ContextCategory.Coding, "code.exe", false, false, false, false, false, false)]
    [Xunit.InlineData(ContextCategory.Meeting, null, true, false, false, false, false, false)]
    [Xunit.InlineData(ContextCategory.Gaming, null, false, true, false, false, false, false)]
    [Xunit.InlineData(ContextCategory.Browsing, "chrome.exe", false, false, false, false, false, false)]
    [Xunit.InlineData(ContextCategory.FocusSession, null, false, false, false, true, false, false)]
    [Xunit.InlineData(ContextCategory.Presentation, null, false, false, true, false, false, false)]
    [Xunit.InlineData(ContextCategory.Offline, null, false, false, false, false, true, false)]
    [Xunit.InlineData(ContextCategory.BatterySaver, null, false, false, false, false, false, true)]
    public void ContextEvidenceMapsToDeclaredCategories(
        ContextCategory expected, string? app, bool meeting, bool gaming, bool presentation,
        bool focus, bool offline, bool batterySaver)
    {
        var snapshot = new ContextEngine().Evaluate(new ContextInput(
            ActiveApplication: app,
            IsMeeting: meeting,
            IsGaming: gaming,
            IsPresentation: presentation,
            IsFocusSession: focus,
            IsOffline: offline,
            IsBatterySaver: batterySaver));

        snapshot.Category.Should().Be(expected);
        snapshot.Confidence.Should().BeInRange(0, 1);
        snapshot.IsAvailable.Should().BeTrue();
    }

    [Xunit.Fact]
    public void ContextSupportsCreativeAndStudyingOnlyThroughExplicitTypedEvidence()
    {
        var engine = new ContextEngine();
        engine.Evaluate(new ContextInput(AdditionalEvidence: new HashSet<ContextCategory> { ContextCategory.CreativeWork }))
            .Category.Should().Be(ContextCategory.CreativeWork);
        engine.Evaluate(new ContextInput(AdditionalEvidence: new HashSet<ContextCategory> { ContextCategory.Studying }))
            .Category.Should().Be(ContextCategory.Studying);
    }

    [Xunit.Fact]
    public void ContextContradictionsUseDeterministicCategoryTieBreakAndWeightedConfidence()
    {
        var snapshot = new ContextEngine().Evaluate(new ContextInput(IsMeeting: true, IsGaming: true));

        snapshot.Category.Should().Be(ContextCategory.Meeting);
        snapshot.Confidence.Should().Be(0.5);
        snapshot.Evidence.Select(item => item.Category).Should().ContainInOrder(ContextCategory.Meeting, ContextCategory.Gaming);
    }

    [Xunit.Fact]
    public void NoEvidenceAndUnavailableEvidenceRemainUnknownAndConservative()
    {
        var noEvidence = new ContextEngine().Evaluate(new ContextInput(IsUserActive: false));
        noEvidence.Category.Should().Be(ContextCategory.Unknown);
        noEvidence.Confidence.Should().Be(0);
        noEvidence.IsAvailable.Should().BeFalse();

        var unavailable = new ContextEngine().Evaluate(new ContextInput(
            IsMeeting: true,
            IsUserActive: false,
            UnavailableSignals: new HashSet<string> { "meeting" }));
        unavailable.Category.Should().Be(ContextCategory.Unknown);
        unavailable.Confidence.Should().Be(0);
        unavailable.IsAvailable.Should().BeFalse();
    }

    [Xunit.Fact]
    public void ContextRejectsInvalidDeclaredEvidenceAndNormalizesAppWhitespace()
    {
        var normalized = new ContextEngine().Evaluate(new ContextInput(ActiveApplication: "  code.exe  "));
        normalized.Category.Should().Be(ContextCategory.Coding);

        FluentActions.Invoking(() => new ContextEngine().Evaluate(new ContextInput(
                AdditionalEvidence: new HashSet<ContextCategory> { (ContextCategory)999 })))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Xunit.Fact]
    public void PriorityPrecedenceAndOutcomesAreExplainable()
    {
        var engine = new PriorityEngine();
        var critical = engine.Evaluate(new PriorityInput("critical", PriorityClass.Low, IsSystemCritical: true));
        var merged = engine.Evaluate(new PriorityInput("repeat", PriorityClass.Medium, IsRepeated: true, CanMerge: true));
        var low = engine.Evaluate(new PriorityInput("low"));

        critical.Classification.Should().Be(PriorityClass.Critical);
        critical.Outcome.Should().Be(PriorityOutcome.Surface);
        critical.IsCritical.Should().BeTrue();
        merged.Outcome.Should().Be(PriorityOutcome.Merge);
        low.Outcome.Should().Be(PriorityOutcome.Silent);
        critical.Reason.Should().Contain("System criticality");
    }

    [Xunit.Fact]
    public void PriorityRespectsOrderedCriticalityUserPriorityAndUrgency()
    {
        var engine = new PriorityEngine();
        var critical = engine.Evaluate(new PriorityInput("critical", PriorityClass.High, IsSystemCritical: true));
        var userPriority = engine.Evaluate(new PriorityInput("user", PriorityClass.Low, IsUserDeclaredPriority: true));
        var generic = engine.Evaluate(new PriorityInput("generic", PriorityClass.Low));
        var urgent = engine.Evaluate(new PriorityInput("urgent", PriorityClass.Low, IsTimeSensitive: true));

        critical.Classification.Should().Be(PriorityClass.Critical);
        userPriority.Classification.Should().Be(PriorityClass.High);
        generic.Classification.Should().Be(PriorityClass.Low);
        urgent.Classification.Should().Be(PriorityClass.High);
    }

    [Xunit.Fact]
    public void ContextRelevanceThenActionabilityAffectMediumPriority()
    {
        var engine = new PriorityEngine();
        var relevant = engine.Evaluate(new PriorityInput("meeting-reminder", PriorityClass.Low,
            Context: new ContextSnapshot(ContextCategory.Meeting, 1, [], true)));
        var irrelevant = engine.Evaluate(new PriorityInput("generic", PriorityClass.Low,
            Context: new ContextSnapshot(ContextCategory.Meeting, 1, [], true)));
        var actionable = engine.Evaluate(new PriorityInput("generic-action", PriorityClass.Low, IsActionable: true));

        relevant.Classification.Should().Be(PriorityClass.Medium);
        relevant.Outcome.Should().Be(PriorityOutcome.Surface);
        irrelevant.Classification.Should().Be(PriorityClass.Low);
        actionable.Classification.Should().Be(PriorityClass.Medium);
        actionable.Outcome.Should().Be(PriorityOutcome.Surface);
    }

    [Xunit.Fact]
    public void NoveltyAndRecencyResolveLowPriorityAndExpirationDeterministically()
    {
        var engine = new PriorityEngine();
        var now = DateTimeOffset.Parse("2025-01-01T00:00:00Z");
        var novel = engine.Evaluate(new PriorityInput("new", EvaluatedAtUtc: now));
        var duplicate = engine.Evaluate(new PriorityInput("duplicate", IsNovel: false, EvaluatedAtUtc: now));
        var recent = engine.Evaluate(new PriorityInput("recent", OccurredAtUtc: now.AddMinutes(-1), EvaluatedAtUtc: now,
            MaximumAge: TimeSpan.FromMinutes(2), IsActionable: true));
        var stale = engine.Evaluate(new PriorityInput("stale", OccurredAtUtc: now.AddMinutes(-3), EvaluatedAtUtc: now,
            MaximumAge: TimeSpan.FromMinutes(2), IsSystemCritical: true));

        novel.Outcome.Should().Be(PriorityOutcome.Silent);
        duplicate.Outcome.Should().Be(PriorityOutcome.Queue);
        recent.Outcome.Should().Be(PriorityOutcome.Surface);
        stale.Outcome.Should().Be(PriorityOutcome.Expire);
    }

    [Xunit.Fact]
    public void PriorityOutcomesIncludeMergeQueueSuppressAndExpire()
    {
        var engine = new PriorityEngine();
        engine.Evaluate(new PriorityInput("merge", PriorityClass.Medium, IsRepeated: true, CanMerge: true))
            .Outcome.Should().Be(PriorityOutcome.Merge);
        engine.Evaluate(new PriorityInput("queue", PriorityClass.Medium))
            .Outcome.Should().Be(PriorityOutcome.Queue);
        engine.Evaluate(new PriorityInput("suppress", PriorityClass.Critical, PolicyAllowed: false))
            .Outcome.Should().Be(PriorityOutcome.Suppress);
        engine.Evaluate(new PriorityInput("expire", IsExpired: true, PolicyAllowed: false))
            .Outcome.Should().Be(PriorityOutcome.Expire);
    }

    [Xunit.Fact]
    public void PriorityRejectsInvalidInputAndContextRestraintSuppressesNonCriticalItems()
    {
        var engine = new PriorityEngine();
        FluentActions.Invoking(() => engine.Evaluate(new PriorityInput(" ")))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => engine.Evaluate(new PriorityInput("invalid", (PriorityClass)99)))
            .Should().Throw<ArgumentOutOfRangeException>();

        engine.Evaluate(new PriorityInput("ordinary", PriorityClass.High,
                Context: new ContextSnapshot(ContextCategory.Gaming, 1, [], true)))
            .Outcome.Should().Be(PriorityOutcome.Queue);
        engine.Evaluate(new PriorityInput("critical", Context: new ContextSnapshot(ContextCategory.Gaming, 1, [], true),
                IsSystemCritical: true))
            .Outcome.Should().Be(PriorityOutcome.Surface);
    }

    [Xunit.Fact]
    public void PolicyFailsClosedForSensitiveAndUnknownExternalAccess()
    {
        var policy = new PolicyEvaluator();

        policy.Evaluate(new PolicyRequest(PolicyResource.Surface, "show")).Decision.Should().Be(PolicyDecision.Allowed);
        policy.Evaluate(new PolicyRequest(PolicyResource.ExternalIntegration, "connect", AuthorizationKnown: false)).Decision.Should().Be(PolicyDecision.Unknown);
        policy.Evaluate(new PolicyRequest(PolicyResource.ExternalIntegration, "connect")).Decision.Should().Be(PolicyDecision.Denied);
        policy.Evaluate(new PolicyRequest(PolicyResource.SensitiveData, "read", IsSensitive: true)).Decision.Should().Be(PolicyDecision.Restricted);
        policy.Evaluate(new PolicyRequest(PolicyResource.SensitiveData, "read", IsSensitive: true, AuthorizationKnown: false)).Decision.Should().Be(PolicyDecision.Unknown);
        policy.Evaluate(new PolicyRequest(PolicyResource.SensitiveData, "read", IsSensitive: true, IsAuthorized: true)).Decision.Should().Be(PolicyDecision.Allowed);

        var denied = policy.Evaluate(new PolicyRequest(PolicyResource.Surface, "show", AuthorizationKnown: false));
        denied.CanSurface.Should().BeFalse();
        denied.Decision.Should().Be(PolicyDecision.Unknown);

        var pipeline = new RuntimeDecisionPipeline(new ContextEngine(), new PriorityEngine(), policy, new TestSettingsStore(new()), new StateMachine());
        var result = pipeline.Evaluate(new RuntimeEvaluationInput(
            new ContextInput(),
            new PriorityInput("critical", IsSystemCritical: true),
            new PolicyRequest(PolicyResource.ExternalIntegration, "connect")));
        result.Policy.Decision.Should().Be(PolicyDecision.Denied);
        result.Priority.Outcome.Should().Be(PriorityOutcome.Suppress);
        result.StateTransitionRequested.Should().BeFalse();
    }

    [Xunit.Fact]
    public async Task SettingsDefaultsPersistenceValidationAndNotificationsAreDeterministic()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"aura-settings-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "settings.json");
        try
        {
            var store = new JsonSettingsStore(path);
            store.Current.Should().Be(new AuraSettings());
            (await store.LoadAsync()).Should().Be(new AuraSettings());

            AuraSettings? notified = null;
            store.Changed += (_, value) => notified = value;
            await store.SaveAsync(new AuraSettings(
                SurfaceEnabled: false,
                ReducedMotion: true,
                AllowSensitiveData: true,
                RetentionDays: 9999,
                PrioritySources: new HashSet<string> { " mail ", "" }));
            var loaded = await store.LoadAsync();

            loaded.ReducedMotion.Should().BeTrue();
            loaded.SurfaceEnabled.Should().BeFalse();
            loaded.AllowSensitiveData.Should().BeTrue();
            loaded.RetentionDays.Should().Be(3650);
            loaded.EffectivePrioritySources.Should().Contain("mail").And.NotContain("");
            notified.Should().NotBeNull();
            notified!.SurfaceEnabled.Should().BeFalse();
            notified.RetentionDays.Should().Be(3650);
            notified.EffectivePrioritySources.Should().Contain("mail");

            await File.WriteAllTextAsync(path, "{ malformed");
            (await store.LoadAsync()).Should().Be(loaded);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Xunit.Fact]
    public async Task MissingAndUnavailableSettingsStorageUseCurrentDefaultsSafely()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "settings.json");
        var missing = new JsonSettingsStore(missingPath);
        (await missing.LoadAsync()).Should().Be(new AuraSettings());

        var directoryPath = Path.Combine(Path.GetTempPath(), $"aura-settings-dir-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directoryPath);
        try
        {
            var unavailable = new JsonSettingsStore(directoryPath);
            (await unavailable.LoadAsync()).Should().Be(new AuraSettings());
            await FluentActions.Awaiting(() => unavailable.SaveAsync(new AuraSettings()).AsTask())
                .Should().ThrowAsync<UnauthorizedAccessException>();
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Xunit.Fact]
    public void SettingsAffectOnlyTheirDecisionBoundaries()
    {
        var machine = new StateMachine();
        var pipeline = new RuntimeDecisionPipeline(new ContextEngine(), new PriorityEngine(), new PolicyEvaluator(),
            new TestSettingsStore(new AuraSettings(SurfaceEnabled: true, AllowSensitiveData: false,
                PrioritySources: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "trusted-source" })), machine);

        var userPreferred = pipeline.Evaluate(new RuntimeEvaluationInput(new ContextInput(),
            new PriorityInput("trusted-source"), new PolicyRequest(PolicyResource.Surface, "show")));
        userPreferred.Priority.Classification.Should().Be(PriorityClass.High);

        var sensitive = pipeline.Evaluate(new RuntimeEvaluationInput(new ContextInput(),
            new PriorityInput("sensitive", IsSystemCritical: true),
            new PolicyRequest(PolicyResource.SensitiveData, "read", IsSensitive: true, IsAuthorized: true)));
        sensitive.Priority.Outcome.Should().Be(PriorityOutcome.Suppress);
        sensitive.StateTransitionRequested.Should().BeFalse();

        var disabledMachine = new StateMachine();
        var disabledPipeline = new RuntimeDecisionPipeline(new ContextEngine(), new PriorityEngine(), new PolicyEvaluator(),
            new TestSettingsStore(new AuraSettings(SurfaceEnabled: false)), disabledMachine);
        disabledPipeline.Evaluate(new RuntimeEvaluationInput(new ContextInput(), new PriorityInput("critical", IsSystemCritical: true),
            new PolicyRequest(PolicyResource.Surface, "show"))).Priority.Outcome.Should().Be(PriorityOutcome.Suppress);
        disabledMachine.Current.Should().Be(AuraState.Idle);
    }

    private sealed class TestSettingsStore(AuraSettings settings) : ISettingsStore
    {
        public AuraSettings Current { get; } = settings;
        public event EventHandler<AuraSettings>? Changed
        {
            add { }
            remove { }
        }
        public ValueTask<AuraSettings> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(Current);
        public ValueTask SaveAsync(AuraSettings value, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
