using Aura.Runtime.Context;
using Aura.Runtime.Policy;
using Aura.Runtime.Priority;
using Aura.Runtime.Settings;
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

    [Xunit.Fact]
    public void ContextContradictionsUseDeterministicCategoryTieBreak()
    {
        var snapshot = new ContextEngine().Evaluate(new ContextInput(IsMeeting: true, IsGaming: true));

        snapshot.Category.Should().Be(ContextCategory.Meeting);
        snapshot.Confidence.Should().Be(0.5);
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
    public void PolicyFailsClosedForSensitiveAndUnknownExternalAccess()
    {
        var policy = new PolicyEvaluator();

        policy.Evaluate(new PolicyRequest(PolicyResource.SensitiveData, "read", true)).Decision.Should().Be(PolicyDecision.Restricted);
        policy.Evaluate(new PolicyRequest(PolicyResource.ExternalIntegration, "connect")).Decision.Should().Be(PolicyDecision.Unknown);
        policy.Evaluate(new PolicyRequest(PolicyResource.Surface, "show")).IsAllowed.Should().BeTrue();
    }

    [Xunit.Fact]
    public async Task SettingsRoundTripAndValidateMalformedValues()
    {
        var path = Path.Combine(Path.GetTempPath(), $"aura-settings-{Guid.NewGuid():N}.json");
        try
        {
            var store = new JsonSettingsStore(path);
            await store.SaveAsync(new AuraSettings(RetentionDays: 9999, ReducedMotion: true));
            var loaded = await store.LoadAsync();

            loaded.ReducedMotion.Should().BeTrue();
            loaded.RetentionDays.Should().Be(3650);
            await File.WriteAllTextAsync(path, "{ malformed");
            (await store.LoadAsync()).Should().Be(loaded);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
