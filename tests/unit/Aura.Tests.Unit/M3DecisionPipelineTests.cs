using Aura.Runtime;
using Aura.Runtime.Context;
using Aura.Runtime.Decision;
using Aura.Runtime.Policy;
using Aura.Runtime.Priority;
using Aura.Runtime.Settings;
using Aura.App.Bootstrap;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
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
    public async Task ProductionDiGraphRoutesAllowedRequestThroughStateMachine()
    {
        using var host = CreateProductionHost(new AuraSettings());
        var runtime = host.Services.GetRequiredService<IRuntimeCore>();
        var dispatcher = host.Services.GetRequiredService<IEventDispatcher>();
        var stateMachine = host.Services.GetRequiredService<IStateMachine>();
        var pipeline = host.Services.GetRequiredService<IRuntimeDecisionPipeline>();

        await host.StartAsync();
        await PublishAsync(dispatcher, new ContextInput(IsMeeting: true),
            new PriorityInput("meeting-reminder", PriorityClass.Medium, IsActionable: true),
            new PolicyRequest(PolicyResource.Surface, "show"));

        runtime.LastEvaluation.Should().BeOfType<RuntimeEvaluationResult.Success>();
        var decision = ((RuntimeEvaluationResult.Success)runtime.LastEvaluation!).Decision;
        decision.Context.Category.Should().Be(ContextCategory.Meeting);
        decision.Priority.Outcome.Should().Be(PriorityOutcome.Surface);
        decision.Policy.Decision.Should().Be(PolicyDecision.Allowed);
        stateMachine.Current.Should().Be(AuraState.Surfacing);
        host.Services.GetRequiredService<IContextEngine>().Current.Should().Be(decision.Context);
        host.Services.GetRequiredService<IRuntimeDecisionPipeline>().Should().BeSameAs(pipeline);
        await host.StopAsync();
    }

    [Xunit.Fact]
    public async Task ProductionDiGraphDeniesPolicyAndSettingsWithoutChangingState()
    {
        using var host = CreateProductionHost(new AuraSettings());
        var dispatcher = host.Services.GetRequiredService<IEventDispatcher>();
        var stateMachine = host.Services.GetRequiredService<IStateMachine>();
        await host.StartAsync();

        await PublishAsync(dispatcher, new ContextInput(), new PriorityInput("critical", IsSystemCritical: true),
            new PolicyRequest(PolicyResource.ExternalIntegration, "connect"));
        var decision = ((RuntimeEvaluationResult.Success)host.Services.GetRequiredService<IRuntimeCore>().LastEvaluation!).Decision;
        decision.Policy.Decision.Should().Be(PolicyDecision.Denied);
        decision.Priority.Outcome.Should().Be(PriorityOutcome.Suppress);
        stateMachine.Current.Should().Be(AuraState.Idle);

        await host.StopAsync();
    }

    [Xunit.Fact]
    public async Task ProductionPrioritySourcesChangeClassificationWhenSurfaceEnabled()
    {
        var unconfiguredHost = CreateProductionHost(new AuraSettings(SurfaceEnabled: true));
        var configuredHost = CreateProductionHost(new AuraSettings(
            SurfaceEnabled: true,
            PrioritySources: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "preferred" }));
        using (unconfiguredHost)
        using (configuredHost)
        {
            await unconfiguredHost.StartAsync();
            await configuredHost.StartAsync();
            await PublishAsync(unconfiguredHost.Services.GetRequiredService<IEventDispatcher>(), new ContextInput(),
                new PriorityInput("preferred"), new PolicyRequest(PolicyResource.Surface, "show"));
            await PublishAsync(configuredHost.Services.GetRequiredService<IEventDispatcher>(), new ContextInput(),
                new PriorityInput("preferred"), new PolicyRequest(PolicyResource.Surface, "show"));

            var baseline = ((RuntimeEvaluationResult.Success)unconfiguredHost.Services.GetRequiredService<IRuntimeCore>().LastEvaluation!).Decision;
            var preferred = ((RuntimeEvaluationResult.Success)configuredHost.Services.GetRequiredService<IRuntimeCore>().LastEvaluation!).Decision;
            baseline.Priority.Classification.Should().Be(PriorityClass.Low);
            preferred.Priority.Classification.Should().Be(PriorityClass.High);
            baseline.Priority.Outcome.Should().Be(PriorityOutcome.Silent);
            preferred.Priority.Outcome.Should().Be(PriorityOutcome.Surface);
            unconfiguredHost.Services.GetRequiredService<IStateMachine>().Current.Should().Be(AuraState.Idle);
            configuredHost.Services.GetRequiredService<IStateMachine>().Current.Should().Be(AuraState.Surfacing);

            await unconfiguredHost.StopAsync();
            await configuredHost.StopAsync();
        }
    }

    [Xunit.Fact]
    public async Task ProductionSurfaceDisabledSuppressesEveryPrioritySourceConfiguration()
    {
        var settingsConfigurations = new[]
        {
            new AuraSettings(SurfaceEnabled: false),
            new AuraSettings(SurfaceEnabled: false, PrioritySources: new HashSet<string> { "preferred" }),
            new AuraSettings(SurfaceEnabled: false, PrioritySources: new HashSet<string> { "other", "preferred" })
        };

        foreach (var settings in settingsConfigurations)
        {
            using var host = CreateProductionHost(settings);
            await host.StartAsync();
            await PublishAsync(host.Services.GetRequiredService<IEventDispatcher>(), new ContextInput(),
                new PriorityInput("preferred", IsSystemCritical: true), new PolicyRequest(PolicyResource.Surface, "show"));
            var decision = ((RuntimeEvaluationResult.Success)host.Services.GetRequiredService<IRuntimeCore>().LastEvaluation!).Decision;
            decision.Priority.Outcome.Should().Be(PriorityOutcome.Suppress);
            decision.Priority.ShouldSurface.Should().BeFalse();
            host.Services.GetRequiredService<IStateMachine>().Current.Should().Be(AuraState.Idle);
            await host.StopAsync();
        }
    }

    [Xunit.Fact]
    public async Task ProductionDiGraphMergesDuplicateAndUsesContextForPriority()
    {
        using var host = CreateProductionHost(new AuraSettings());
        var dispatcher = host.Services.GetRequiredService<IEventDispatcher>();
        await host.StartAsync();

        await PublishAsync(dispatcher, new ContextInput(IsMeeting: true),
            new PriorityInput("meeting-followup", IsRepeated: true, CanMerge: true),
            new PolicyRequest(PolicyResource.Surface, "show"));
        var decision = ((RuntimeEvaluationResult.Success)host.Services.GetRequiredService<IRuntimeCore>().LastEvaluation!).Decision;
        decision.Context.Category.Should().Be(ContextCategory.Meeting);
        decision.Priority.Outcome.Should().Be(PriorityOutcome.Merge);
        decision.Priority.Context.Should().Be(ContextCategory.Meeting);

        await host.StopAsync();
    }

    [Xunit.Fact]
    public async Task ProductionDiGraphFailsSafelyWhenEvaluationInputIsInvalid()
    {
        using var host = CreateProductionHost(new AuraSettings());
        await host.StartAsync();
        var dispatcher = host.Services.GetRequiredService<IEventDispatcher>();
        var stateMachine = host.Services.GetRequiredService<IStateMachine>();

        await PublishAsync(dispatcher, new ContextInput(), new PriorityInput(" "),
            new PolicyRequest(PolicyResource.Surface, "show"));
        var decision = host.Services.GetRequiredService<IRuntimeCore>().LastEvaluation!;
        decision.Should().BeOfType<RuntimeEvaluationResult.FailureResult>();
        host.Services.GetRequiredService<IRuntimeCore>().Health.Should().Be(RuntimeHealth.Degraded);
        stateMachine.Current.Should().Be(AuraState.Idle);
        await host.StopAsync();
    }

    [Xunit.Fact]
    public async Task ProductionDiGraphFailureIsDistinctAndLeavesRegisteredStateUnchangedAfterPositiveControl()
    {
        var throwingContext = new ThrowingContextEngine();
        using var host = CreateProductionHost(new AuraSettings(), throwingContext);
        var runtime = host.Services.GetRequiredService<IRuntimeCore>();
        var dispatcher = host.Services.GetRequiredService<IEventDispatcher>();
        var pipeline = host.Services.GetRequiredService<IRuntimeDecisionPipeline>();
        var stateMachine = host.Services.GetRequiredService<IStateMachine>();
        pipeline.Should().BeSameAs(host.Services.GetRequiredService<IRuntimeDecisionPipeline>());
        runtime.Should().BeSameAs(host.Services.GetRequiredService<IRuntimeCore>());
        stateMachine.Should().BeSameAs(host.Services.GetRequiredService<IStateMachine>());
        var pipelineStateMachine = typeof(RuntimeDecisionPipeline)
            .GetField("<stateMachine>P", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(pipeline);
        pipelineStateMachine.Should().BeSameAs(stateMachine);

        await host.StartAsync();
        await PublishAsync(dispatcher, new ContextInput(), new PriorityInput("positive", IsSystemCritical: true),
            new PolicyRequest(PolicyResource.Surface, "show"));
        runtime.LastEvaluation.Should().BeOfType<RuntimeEvaluationResult.Success>();
        stateMachine.Current.Should().Be(AuraState.Surfacing);
        stateMachine.TryTransition(AuraState.Compact).Should().BeTrue();
        stateMachine.TryTransition(AuraState.Idle).Should().BeTrue();

        throwingContext.ThrowOnNextEvaluation = true;
        var input = new RuntimeEvaluationInput(new ContextInput(), new PriorityInput("failure"),
            new PolicyRequest(PolicyResource.Surface, "show"));
        var direct = runtime.Evaluate(input);
        direct.Should().BeOfType<RuntimeEvaluationResult.FailureResult>();
        ((RuntimeEvaluationResult.FailureResult)direct).Error.Should().BeOfType<InvalidOperationException>();
        ((RuntimeEvaluationResult.FailureResult)direct).Should().NotBeAssignableTo<RuntimeEvaluationResult.Success>();
        throwingContext.InvocationCount.Should().BeGreaterThanOrEqualTo(1);
        stateMachine.Current.Should().Be(AuraState.Idle);

        throwingContext.ThrowOnNextEvaluation = true;
        await dispatcher.PublishAsync(new RuntimeEvaluationRequestedEvent(DateTimeOffset.UtcNow, input));
        var eventResult = runtime.LastEvaluation.Should().BeOfType<RuntimeEvaluationResult.FailureResult>().Subject;
        eventResult.Error.Should().BeOfType<InvalidOperationException>();
        eventResult.Should().NotBeAssignableTo<RuntimeEvaluationResult.Success>();
        throwingContext.InvocationCount.Should().BeGreaterThanOrEqualTo(2);
        runtime.Health.Should().Be(RuntimeHealth.Degraded);
        stateMachine.Current.Should().Be(AuraState.Idle);
        await host.StopAsync();
    }

    [Xunit.Fact]
    public async Task ProductionDiGraphCancellationAndRestartDoNotDuplicateEvents()
    {
        using var host = CreateProductionHost(new AuraSettings());
        var dispatcher = host.Services.GetRequiredService<IEventDispatcher>();
        var runtime = host.Services.GetRequiredService<IRuntimeCore>();

        using (var canceled = new CancellationTokenSource())
        {
            canceled.Cancel();
            await FluentActions.Awaiting(() => runtime.StartAsync(canceled.Token).AsTask())
                .Should().ThrowAsync<OperationCanceledException>();
        }

        await host.StartAsync();
        await host.StopAsync();
        await runtime.StartAsync();
        await PublishAsync(dispatcher, new ContextInput(), new PriorityInput("critical", IsSystemCritical: true),
            new PolicyRequest(PolicyResource.Surface, "show"));
        runtime.LastEvaluation.Should().BeOfType<RuntimeEvaluationResult.Success>();
        await runtime.StopAsync();
    }

    private static IHost CreateProductionHost(AuraSettings settings, ThrowingContextEngine? contextOverride = null)
    {
        return new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddAuraRuntime();
                services.AddSingleton<ISettingsStore>(new FixedSettingsStore(settings));
                if (contextOverride is not null)
                {
                    services.AddSingleton<IContextEngine>(contextOverride);
                }
            })
            .Build();
    }

    private static ValueTask PublishAsync(IEventDispatcher dispatcher, ContextInput context, PriorityInput priority, PolicyRequest policy)
        => dispatcher.PublishAsync(new RuntimeEvaluationRequestedEvent(DateTimeOffset.UtcNow,
            new RuntimeEvaluationInput(context, priority, policy)));

    private sealed class ThrowingContextEngine : IContextEngine
    {
        private readonly ContextEngine _inner = new();
        public bool ThrowOnNextEvaluation { get; set; }
        public int InvocationCount { get; private set; }
        public ContextSnapshot Current => _inner.Current;
        public event EventHandler<ContextChangedEventArgs>? ContextChanged
        {
            add => _inner.ContextChanged += value;
            remove => _inner.ContextChanged -= value;
        }

        public ContextSnapshot Evaluate(ContextInput input)
        {
            InvocationCount++;
            if (ThrowOnNextEvaluation)
            {
                ThrowOnNextEvaluation = false;
                throw new InvalidOperationException("Injected context failure.");
            }
            return _inner.Evaluate(input);
        }
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
