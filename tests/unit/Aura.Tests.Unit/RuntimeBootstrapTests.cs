using Aura.App.Bootstrap;
using Aura.Runtime;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Aura.Runtime.Decision;
using Aura.Runtime.Context;
using Aura.Runtime.Policy;
using Aura.Runtime.Priority;
using Aura.Runtime.Settings;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aura.Tests.Unit;

public sealed class RuntimeBootstrapTests
{
    [Xunit.Fact]
    public async Task BootstrapHost_ComposesAndStarts()
    {
        using var host = BootstrapHostFactory.CreateHost();

        host.Services.GetRequiredService<IConfiguration>().Should().NotBeNull();
        host.Services.GetRequiredService<IRuntimeCore>().Should().NotBeNull();
        host.Services.GetRequiredService<IEventDispatcher>().Should().NotBeNull();
        host.Services.GetRequiredService<IRuntimeScheduler>().Should().NotBeNull();

        await host.StartAsync();

        var runtime = host.Services.GetRequiredService<IRuntimeCore>();
        runtime.State.Should().Be(RuntimeState.Running);
        runtime.Health.Should().Be(RuntimeHealth.Healthy);

        await host.StopAsync();
    }

    [Xunit.Fact]
    public async Task RuntimeCore_StartsAndStopsDeterministically()
    {
        var eventDispatcher = new EventDispatcher();
        var runtime = CreateRuntime(eventDispatcher);

        await runtime.StartAsync();
        runtime.State.Should().Be(RuntimeState.Running);
        runtime.Health.Should().Be(RuntimeHealth.Healthy);

        await runtime.StopAsync();
        runtime.State.Should().Be(RuntimeState.Stopped);
    }

    [Xunit.Fact]
    public async Task RuntimeCore_CancellationDuringStartCompletesWithoutFaulting()
    {
        var eventDispatcher = new EventDispatcher();
        var runtime = CreateRuntime(eventDispatcher);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await FluentActions.Awaiting(() => runtime.StartAsync(cts.Token).AsTask())
            .Should().ThrowAsync<OperationCanceledException>();
        runtime.Health.Should().Be(RuntimeHealth.Degraded);
    }

    [Xunit.Fact]
    public async Task EventDispatcher_PublishesToSubscribersInOrder()
    {
        var dispatcher = new EventDispatcher();
        var observed = new List<string>();

        using var first = dispatcher.Subscribe((_, _) =>
        {
            observed.Add("first");
            return ValueTask.CompletedTask;
        });

        using var second = dispatcher.Subscribe((_, _) =>
        {
            observed.Add("second");
            return ValueTask.CompletedTask;
        });

        await dispatcher.PublishAsync(new RuntimeStartedEvent(DateTimeOffset.UtcNow));

        observed.Should().ContainInOrder("first", "second");
    }

    private static RuntimeCore CreateRuntime(IEventDispatcher dispatcher)
    {
        var stateMachine = new StateMachine();
        return new RuntimeCore(
            dispatcher,
            NullLogger<RuntimeCore>.Instance,
            new RuntimeDecisionPipeline(
                new ContextEngine(),
                new PriorityEngine(),
                new PolicyEvaluator(),
                new JsonSettingsStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "settings.json")),
                stateMachine));
    }

    [Xunit.Fact]
    public async Task RuntimeCoreDirectAndEventEvaluationFailuresAreDistinctFromDomainResults()
    {
        var dispatcher = new EventDispatcher();
        var pipeline = new ThrowingDecisionPipeline();
        var runtime = new RuntimeCore(dispatcher, NullLogger<RuntimeCore>.Instance, pipeline);
        await runtime.StartAsync();
        var input = new RuntimeEvaluationInput(new ContextInput(), new PriorityInput("failure"),
            new PolicyRequest(PolicyResource.Surface, "show"));

        var direct = runtime.Evaluate(input);
        direct.Should().BeOfType<RuntimeEvaluationResult.FailureResult>();
        ((RuntimeEvaluationResult.FailureResult)direct).Error.Should().BeOfType<InvalidOperationException>();
        runtime.LastEvaluation.Should().BeSameAs(direct);

        await dispatcher.PublishAsync(new RuntimeEvaluationRequestedEvent(DateTimeOffset.UtcNow, input));
        var eventResult = runtime.LastEvaluation.Should().BeOfType<RuntimeEvaluationResult.FailureResult>().Subject;
        eventResult.Error.Should().BeOfType<InvalidOperationException>();
        pipeline.InvocationCount.Should().Be(2);
        runtime.Health.Should().Be(RuntimeHealth.Degraded);
        await runtime.StopAsync();
    }

    [Xunit.Fact]
    public async Task RuntimeCoreEvaluationCancellationIsNotConvertedToFailure()
    {
        var dispatcher = new EventDispatcher();
        var pipeline = new CancelingDecisionPipeline();
        var runtime = new RuntimeCore(dispatcher, NullLogger<RuntimeCore>.Instance, pipeline);
        await runtime.StartAsync();
        using var cancellation = new CancellationTokenSource();
        await FluentActions.Awaiting(() => dispatcher.PublishAsync(new RuntimeEvaluationRequestedEvent(
                DateTimeOffset.UtcNow, new RuntimeEvaluationInput(new ContextInput(), new PriorityInput("cancel"),
                    new PolicyRequest(PolicyResource.Surface, "show"))), cancellation.Token).AsTask())
            .Should().ThrowAsync<OperationCanceledException>();
        pipeline.InvocationCount.Should().Be(1);
        runtime.LastEvaluation.Should().BeNull();
        runtime.Health.Should().Be(RuntimeHealth.Healthy);
        await runtime.StopAsync();
    }

    [Xunit.Fact]
    public async Task RuntimeCoreReleasesAndRecreatesSingleSubscriptionAcrossRestart()
    {
        var dispatcher = new TrackingEventDispatcher();
        var runtime = new RuntimeCore(dispatcher, NullLogger<RuntimeCore>.Instance, new NoopDecisionPipeline());

        await runtime.StartAsync();
        dispatcher.ActiveSubscriptions.Should().Be(1);
        await runtime.StopAsync();
        dispatcher.ActiveSubscriptions.Should().Be(0);

        await runtime.StartAsync();
        dispatcher.ActiveSubscriptions.Should().Be(1);
        dispatcher.TotalSubscriptions.Should().Be(2);
        await runtime.StopAsync();
        dispatcher.ActiveSubscriptions.Should().Be(0);
    }

    [Xunit.Fact]
    public async Task RuntimeCoreCleansSubscriptionAfterFailedStartupAndShutdown()
    {
        var dispatcher = new TrackingEventDispatcher { FailPublish = true };
        var runtime = new RuntimeCore(dispatcher, NullLogger<RuntimeCore>.Instance, new NoopDecisionPipeline());
        await FluentActions.Awaiting(() => runtime.StartAsync().AsTask()).Should().ThrowAsync<InvalidOperationException>();
        dispatcher.ActiveSubscriptions.Should().Be(0);

        dispatcher.FailPublish = false;
        await runtime.StartAsync();
        dispatcher.FailPublish = true;
        await FluentActions.Awaiting(() => runtime.StopAsync().AsTask()).Should().ThrowAsync<InvalidOperationException>();
        dispatcher.ActiveSubscriptions.Should().Be(0);
    }

    [Xunit.Fact]
    public async Task RuntimeCoreCleansSubscriptionAfterCanceledStartupAndShutdown()
    {
        var dispatcher = new TrackingEventDispatcher();
        var runtime = new RuntimeCore(dispatcher, NullLogger<RuntimeCore>.Instance, new NoopDecisionPipeline());
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await FluentActions.Awaiting(() => runtime.StartAsync(canceled.Token).AsTask()).Should().ThrowAsync<OperationCanceledException>();
        dispatcher.ActiveSubscriptions.Should().Be(0);

        await runtime.StartAsync();
        using var canceledStop = new CancellationTokenSource();
        canceledStop.Cancel();
        await FluentActions.Awaiting(() => runtime.StopAsync(canceledStop.Token).AsTask()).Should().ThrowAsync<OperationCanceledException>();
        dispatcher.ActiveSubscriptions.Should().Be(0);
    }

    [Xunit.Fact]
    public async Task RuntimeScheduler_ExecutesQueuedWork()
    {
        using var scheduler = new RuntimeScheduler();
        var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await scheduler.ScheduleAsync(_ =>
        {
            completed.TrySetResult(true);
            return ValueTask.CompletedTask;
        });

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Xunit.Fact]
    public async Task RuntimeCore_ShutdownCanBeCancelled()
    {
        var eventDispatcher = new EventDispatcher();
        var runtime = CreateRuntime(eventDispatcher);

        await runtime.StartAsync();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await FluentActions.Awaiting(() => runtime.StopAsync(cts.Token).AsTask())
            .Should().ThrowAsync<OperationCanceledException>();
        runtime.State.Should().Be(RuntimeState.Stopped);
    }

    [Xunit.Fact]
    public async Task RuntimeCore_StartIsIdempotent()
    {
        var eventDispatcher = new EventDispatcher();
        var runtime = CreateRuntime(eventDispatcher);

        await runtime.StartAsync();
        await runtime.StartAsync();

        runtime.State.Should().Be(RuntimeState.Running);
    }

    [Xunit.Fact]
    public async Task RuntimeCore_StopIsIdempotent()
    {
        var eventDispatcher = new EventDispatcher();
        var runtime = CreateRuntime(eventDispatcher);

        await runtime.StopAsync();
        await runtime.StopAsync();

        runtime.State.Should().Be(RuntimeState.Created);
    }

    [Xunit.Fact]
    public async Task RuntimeCore_ConcurrentStartRequestsCollapseToSingleRunningState()
    {
        var eventDispatcher = new EventDispatcher();
        var runtime = CreateRuntime(eventDispatcher);

        await Task.WhenAll(runtime.StartAsync().AsTask(), runtime.StartAsync().AsTask());

        runtime.State.Should().Be(RuntimeState.Running);
    }

    [Xunit.Fact]
    public async Task RuntimeCore_ConcurrentStopRequestsAreSafe()
    {
        var eventDispatcher = new EventDispatcher();
        var runtime = CreateRuntime(eventDispatcher);

        await runtime.StartAsync();
        await Task.WhenAll(runtime.StopAsync().AsTask(), runtime.StopAsync().AsTask());

        runtime.State.Should().Be(RuntimeState.Stopped);
    }

    [Xunit.Fact]
    public async Task EventDispatcher_CancellationStopsFurtherDelivery()
    {
        var dispatcher = new EventDispatcher();
        var observed = new List<string>();
        using var cts = new CancellationTokenSource();

        using var first = dispatcher.Subscribe((_, token) =>
        {
            observed.Add("first");
            cts.Cancel();
            token.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        });

        using var second = dispatcher.Subscribe((_, _) =>
        {
            observed.Add("second");
            return ValueTask.CompletedTask;
        });

        await FluentActions.Awaiting(() => dispatcher.PublishAsync(new RuntimeStartedEvent(DateTimeOffset.UtcNow), cts.Token).AsTask())
            .Should()
            .ThrowAsync<OperationCanceledException>();

        observed.Should().ContainSingle().Which.Should().Be("first");
    }

    [Xunit.Fact]
    public async Task RuntimeScheduler_CancellationCancelsQueuedWork()
    {
        using var scheduler = new RuntimeScheduler();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await FluentActions.Awaiting(() => scheduler.ScheduleAsync(_ => ValueTask.CompletedTask, cts.Token).AsTask())
            .Should()
            .ThrowAsync<OperationCanceledException>();
    }

    private sealed class NoopDecisionPipeline : IRuntimeDecisionPipeline
    {
        public RuntimeDecision Evaluate(RuntimeEvaluationInput input)
            => new(ContextSnapshot.Unknown,
                new PriorityDecision(PriorityClass.Low, PriorityOutcome.Silent, false, false, "test", ContextCategory.Unknown, true),
                new PolicyResult(PolicyDecision.Allowed, "test"), false);
    }

    private sealed class ThrowingDecisionPipeline : IRuntimeDecisionPipeline
    {
        public int InvocationCount { get; private set; }
        public RuntimeDecision Evaluate(RuntimeEvaluationInput input)
        {
            InvocationCount++;
            throw new InvalidOperationException("evaluation failure");
        }
    }

    private sealed class CancelingDecisionPipeline : IRuntimeDecisionPipeline
    {
        public int InvocationCount { get; private set; }
        public RuntimeDecision Evaluate(RuntimeEvaluationInput input)
        {
            InvocationCount++;
            throw new OperationCanceledException("evaluation canceled");
        }
    }

    private sealed class TrackingEventDispatcher : IEventDispatcher
    {
        private readonly List<Func<RuntimeEvent, CancellationToken, ValueTask>> _handlers = [];
        public int ActiveSubscriptions => _handlers.Count;
        public int TotalSubscriptions { get; private set; }
        public bool FailPublish { get; set; }

        public ValueTask PublishAsync(RuntimeEvent runtimeEvent, CancellationToken cancellationToken = default)
        {
            if (FailPublish) throw new InvalidOperationException("publish failure");
            foreach (var handler in _handlers.ToArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var pending = handler(runtimeEvent, cancellationToken);
                if (!pending.IsCompletedSuccessfully) return pending;
            }
            return ValueTask.CompletedTask;
        }

        public IDisposable Subscribe(Func<RuntimeEvent, CancellationToken, ValueTask> handler)
        {
            _handlers.Add(handler);
            TotalSubscriptions++;
            return new TrackingSubscription(this, handler);
        }

        private sealed class TrackingSubscription(TrackingEventDispatcher owner,
            Func<RuntimeEvent, CancellationToken, ValueTask> handler) : IDisposable
        {
            private int _disposed;
            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0) owner._handlers.Remove(handler);
            }
        }
    }

    [Xunit.Fact]
    public void RuntimeScheduler_DisposeIsIdempotent()
    {
        var scheduler = new RuntimeScheduler();
        scheduler.Dispose();
        scheduler.Dispose();
    }
}
