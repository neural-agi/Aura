using Aura.Runtime;
using Aura.Runtime.Context;
using Aura.Runtime.Decision;
using Aura.Runtime.Policy;
using Aura.Runtime.Priority;
using Aura.Runtime.Settings;
using FluentAssertions;

namespace Aura.Tests.Unit;

public sealed class M3DecisionPipelineTests
{
    [Xunit.Fact]
    public void PipelineEvaluatesContextPriorityPolicyAndRequestsExistingStateTransition()
    {
        var machine = new StateMachine();
        var pipeline = new RuntimeDecisionPipeline(new ContextEngine(), new PriorityEngine(), new PolicyEvaluator(), new FixedSettingsStore(new()), machine);

        var result = pipeline.Evaluate(new RuntimeEvaluationInput(
            new ContextInput(IsMeeting: true),
            new PriorityInput("meeting", PriorityClass.High, IsTimeSensitive: true),
            new PolicyRequest(PolicyResource.Surface, "show")));

        result.Context.Category.Should().Be(ContextCategory.Meeting);
        result.Priority.Outcome.Should().Be(PriorityOutcome.Surface);
        result.StateTransitionRequested.Should().BeTrue();
        machine.Current.Should().Be(AuraState.Surfacing);
    }

    [Xunit.Fact]
    public void PolicyAndSettingsCanPreventStateTransitionRegardlessOfPriority()
    {
        var machine = new StateMachine();
        var pipeline = new RuntimeDecisionPipeline(new ContextEngine(), new PriorityEngine(), new PolicyEvaluator(), new FixedSettingsStore(new(SurfaceEnabled: false)), machine);

        var result = pipeline.Evaluate(new RuntimeEvaluationInput(new ContextInput(), new PriorityInput("critical", IsSystemCritical: true), new PolicyRequest(PolicyResource.Surface, "show")));

        result.Priority.Outcome.Should().Be(PriorityOutcome.Suppress);
        result.StateTransitionRequested.Should().BeFalse();
        machine.Current.Should().Be(AuraState.Idle);
    }

    [Xunit.Fact]
    public async Task RuntimeCoreConsumesEvaluationRequestThroughEventDispatcher()
    {
        var dispatcher = new EventDispatcher();
        var runtime = new RuntimeCore(dispatcher, Microsoft.Extensions.Logging.Abstractions.NullLogger<RuntimeCore>.Instance);

        await dispatcher.PublishAsync(new Aura.Runtime.Decision.RuntimeEvaluationRequestedEvent(
            DateTimeOffset.UtcNow,
            new RuntimeEvaluationInput(
                new ContextInput(IsPresentation: true),
                new PriorityInput("presentation", PriorityClass.High),
                new PolicyRequest(PolicyResource.Surface, "show"))));

        runtime.LastDecision.Should().NotBeNull();
        runtime.LastDecision!.Context.Category.Should().Be(ContextCategory.Presentation);
        runtime.LastDecision.Priority.Outcome.Should().Be(PriorityOutcome.Surface);
    }

    private sealed class FixedSettingsStore(AuraSettings settings) : ISettingsStore
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
