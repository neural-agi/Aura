using Aura.App.Bootstrap;
using Aura.Runtime;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        var runtime = new RuntimeCore(eventDispatcher, Microsoft.Extensions.Logging.Abstractions.NullLogger<RuntimeCore>.Instance);

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
        var runtime = new RuntimeCore(eventDispatcher, Microsoft.Extensions.Logging.Abstractions.NullLogger<RuntimeCore>.Instance);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await runtime.StartAsync(cts.Token);
        runtime.Health.Should().Be(RuntimeHealth.Healthy);
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
        var runtime = new RuntimeCore(eventDispatcher, Microsoft.Extensions.Logging.Abstractions.NullLogger<RuntimeCore>.Instance);

        await runtime.StartAsync();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await runtime.StopAsync(cts.Token);
        runtime.State.Should().Be(RuntimeState.Stopped);
    }

    [Xunit.Fact]
    public async Task RuntimeCore_StartIsIdempotent()
    {
        var eventDispatcher = new EventDispatcher();
        var runtime = new RuntimeCore(eventDispatcher, Microsoft.Extensions.Logging.Abstractions.NullLogger<RuntimeCore>.Instance);

        await runtime.StartAsync();
        await runtime.StartAsync();

        runtime.State.Should().Be(RuntimeState.Running);
    }

    [Xunit.Fact]
    public async Task RuntimeCore_StopIsIdempotent()
    {
        var eventDispatcher = new EventDispatcher();
        var runtime = new RuntimeCore(eventDispatcher, Microsoft.Extensions.Logging.Abstractions.NullLogger<RuntimeCore>.Instance);

        await runtime.StopAsync();
        await runtime.StopAsync();

        runtime.State.Should().Be(RuntimeState.Created);
    }

    [Xunit.Fact]
    public async Task RuntimeCore_ConcurrentStartRequestsCollapseToSingleRunningState()
    {
        var eventDispatcher = new EventDispatcher();
        var runtime = new RuntimeCore(eventDispatcher, Microsoft.Extensions.Logging.Abstractions.NullLogger<RuntimeCore>.Instance);

        await Task.WhenAll(runtime.StartAsync().AsTask(), runtime.StartAsync().AsTask());

        runtime.State.Should().Be(RuntimeState.Running);
    }

    [Xunit.Fact]
    public async Task RuntimeCore_ConcurrentStopRequestsAreSafe()
    {
        var eventDispatcher = new EventDispatcher();
        var runtime = new RuntimeCore(eventDispatcher, Microsoft.Extensions.Logging.Abstractions.NullLogger<RuntimeCore>.Instance);

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

    [Xunit.Fact]
    public void RuntimeScheduler_DisposeIsIdempotent()
    {
        var scheduler = new RuntimeScheduler();
        scheduler.Dispose();
        scheduler.Dispose();
    }
}
