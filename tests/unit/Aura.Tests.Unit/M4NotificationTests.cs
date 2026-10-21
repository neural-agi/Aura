using Aura.Runtime;
using Aura.Runtime.Context;
using Aura.Runtime.Decision;
using Aura.Runtime.Notifications;
using Aura.Runtime.Artifacts;
using Aura.Runtime.Policy;
using Aura.Runtime.Priority;
using Aura.Runtime.Settings;
using Aura.UI;
using Aura.UI.Interop;
using Aura.UI.Surface;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.UI.Xaml;

namespace Aura.Tests.Unit;

public sealed class M4NotificationTests
{
    [Xunit.Fact]
    public void NormalizationPrefersSourceIdAndNormalizesPayload()
    {
        var notification = NotificationNormalizer.Normalize(new NotificationSourcePayload(
            "os-123", "  Mail  ", "  Hello ", " Body ", DateTimeOffset.Parse("2026-10-17T12:00:00Z"),
            [new NotificationAction("open", "Open")]));

        notification.Id.Should().Be("os-123");
        notification.SourceApp.Should().Be("Mail");
        notification.Title.Should().Be("Hello");
        notification.Body.Should().Be("Body");
        notification.Actions.Should().ContainSingle().Which.Id.Should().Be("open");
    }

    [Xunit.Fact]
    public void MissingSourceIdUsesDeterministicSourceTimestampTitleIdentity()
    {
        var timestamp = DateTimeOffset.Parse("2026-10-17T12:00:00Z");
        var first = NotificationNormalizer.Normalize(new NotificationSourcePayload(null, "Mail", "Hello", "body", timestamp));
        var second = NotificationNormalizer.Normalize(new NotificationSourcePayload(null, "Mail", "Hello", "different body", timestamp));
        var different = NotificationNormalizer.Normalize(new NotificationSourcePayload(null, "Mail", "Other", "body", timestamp));

        first.Id.Should().Be(second.Id);
        first.Id.Should().NotBe(different.Id);
        first.Id.Should().HaveLength(64);
    }

    [Xunit.Fact]
    public void ExactIdentityDuplicatesAreSuppressed()
    {
        var identities = new NotificationIdentitySet();
        var notification = NotificationNormalizer.Normalize(new NotificationSourcePayload("os-1", "Mail", "Hello", "body", DateTimeOffset.UtcNow));

        identities.TryAccept(notification).Should().BeTrue();
        identities.TryAccept(notification with { Body = "changed" }).Should().BeFalse();
    }

    [Xunit.Fact]
    public async Task ProductionGraphProjectsOnlySuccessfulAuthorizedNotificationAndReplacesWithoutTransition()
    {
        using var host = CreateHost(new AuraSettings(AllowSensitiveData: true));
        await host.StartAsync();
        var stateMachine = host.Services.GetRequiredService<IStateMachine>();
        var resolvedSink = host.Services.GetRequiredService<INotificationProjectionSink>();
        var runtime = host.Services.GetRequiredService<IRuntimeCore>();
        var dispatcher = host.Services.GetRequiredService<IEventDispatcher>();
        var manager = (SurfaceManager)host.Services.GetRequiredService<ISurfaceManager>();

        stateMachine.TryTransition(AuraState.Surfacing).Should().BeTrue();
        stateMachine.TryTransition(AuraState.Compact).Should().BeTrue();
        var first = CreateNotification("first");
        await dispatcher.PublishAsync(new RuntimeEvaluationRequestedEvent(first.Timestamp, CreateInput(first, authorized: true, prioritySource: true)));
        var evaluated = runtime.LastEvaluation.Should().BeOfType<RuntimeEvaluationResult.Success>().Subject;
        evaluated.Decision.IsAuthorizedForSurface.Should().BeTrue($"{evaluated.Decision.Policy.Decision} / {evaluated.Decision.Priority.Outcome} / {evaluated.Decision.Priority.PolicyAllowed}");
        evaluated.Decision.Priority.Outcome.Should().Be(PriorityOutcome.Surface);
        resolvedSink.Should().BeSameAs(manager);
        manager.Current.Notification.Should().Be(first);
        var stateBeforeReplacement = stateMachine.Current;
        evaluated.Decision.StateTransitionRequested.Should().BeFalse();

        var second = CreateNotification("second");
        var stateBeforeSecond = stateMachine.Current;
        stateBeforeSecond.Should().Be(AuraState.Compact);
        await dispatcher.PublishAsync(new RuntimeEvaluationRequestedEvent(second.Timestamp, CreateInput(second, authorized: true, prioritySource: true)));
        var secondDecision = runtime.LastEvaluation.Should().BeOfType<RuntimeEvaluationResult.Success>().Subject.Decision;
        secondDecision.IsAuthorizedForSurface.Should().BeTrue($"{secondDecision.Policy.Decision}/{secondDecision.Priority.Outcome}/{secondDecision.Priority.PolicyAllowed}");
        manager.Current.Notification.Should().Be(second);
        stateMachine.Current.Should().Be(stateBeforeReplacement);
        secondDecision.StateTransitionRequested.Should().BeFalse();
        manager.Project(CreateNotification("unauthorized"), secondDecision with { IsAuthorizedForSurface = false });
        manager.Current.Notification.Should().Be(second);
        await host.StopAsync();
    }

    [Xunit.Theory]
    [Xunit.InlineData(false, true)]
    [Xunit.InlineData(true, false)]
    public async Task DeniedOrSettingsSuppressedNotificationNeverReachesProjection(bool allowSensitive, bool surfaceEnabled)
    {
        using var host = CreateHost(new AuraSettings(AllowSensitiveData: allowSensitive, SurfaceEnabled: surfaceEnabled));
        await host.StartAsync();
        var notification = CreateNotification("private");
        await host.Services.GetRequiredService<IEventDispatcher>().PublishAsync(
            new RuntimeEvaluationRequestedEvent(notification.Timestamp, CreateInput(notification, authorized: allowSensitive)));

        host.Services.GetRequiredService<ISurfaceManager>().Current.Notification.Should().BeNull();
        host.Services.GetRequiredService<IStateMachine>().Current.Should().Be(AuraState.Idle);
        var result = host.Services.GetRequiredService<IRuntimeCore>().LastEvaluation.Should().BeOfType<RuntimeEvaluationResult.Success>().Subject;
        result.Decision.IsAuthorizedForSurface.Should().BeFalse();
        await host.StopAsync();
    }

    [Xunit.Fact]
    public async Task EvaluationFailureNeverReachesProjection()
    {
        using var host = CreateHost(new AuraSettings(AllowSensitiveData: true), throwingContext: true);
        await host.StartAsync();
        var notification = CreateNotification("throwing");
        await host.Services.GetRequiredService<IEventDispatcher>().PublishAsync(
            new RuntimeEvaluationRequestedEvent(notification.Timestamp, CreateInput(notification, authorized: true)));

        host.Services.GetRequiredService<IRuntimeCore>().LastEvaluation.Should().BeOfType<RuntimeEvaluationResult.FailureResult>();
        host.Services.GetRequiredService<IStateMachine>().Current.Should().Be(AuraState.Idle);
        await host.StopAsync();
    }

    [Xunit.Theory]
    [Xunit.InlineData(NotificationProviderStatus.Unavailable)]
    [Xunit.InlineData(NotificationProviderStatus.AccessDenied)]
    public async Task ProviderUnavailableOrDeniedHasDeterministicNoOp(NotificationProviderStatus status)
    {
        using var host = CreateHost(new AuraSettings(), new FixedNotificationProvider(new(status, [])));
        await host.StartAsync();
        var ingestion = host.Services.GetRequiredService<NotificationIngestionService>();
        (await ingestion.PollOnceAsync(new ContextInput())).Should().Be(status);
        host.Services.GetRequiredService<IRuntimeCore>().LastEvaluation.Should().BeNull();
        host.Services.GetRequiredService<IStateMachine>().Current.Should().Be(AuraState.Idle);
        await host.StopAsync();
    }

    [Xunit.Fact]
    public async Task IngestionSuppressesExactDuplicateBeforePublishing()
    {
        var timestamp = DateTimeOffset.Parse("2026-10-17T12:00:00Z");
        var payload = new NotificationSourcePayload("same-id", "Mail", "Title", "Body", timestamp);
        var provider = new FixedNotificationProvider(new(NotificationProviderStatus.Available, [payload, payload]));
        using var host = CreateHost(new AuraSettings(AllowSensitiveData: true), provider);
        await host.StartAsync();

        (await host.Services.GetRequiredService<NotificationIngestionService>().PollOnceAsync(new ContextInput()))
            .Should().Be(NotificationProviderStatus.Available);
        var last = host.Services.GetRequiredService<IRuntimeCore>().LastEvaluation.Should().BeOfType<RuntimeEvaluationResult.Success>().Subject;
        last.Decision.Priority.Outcome.Should().Be(PriorityOutcome.Suppress);
        last.Decision.Priority.ShouldSurface.Should().BeFalse();
        last.Decision.Policy.Decision.Should().Be(PolicyDecision.Unknown);
        last.Decision.IsAuthorizedForSurface.Should().BeFalse();
        host.Services.GetRequiredService<IStateMachine>().Current.Should().Be(AuraState.Idle);
        await host.StopAsync();
    }

    private static IHost CreateHost(AuraSettings settings,
        INotificationProvider? provider = null, bool throwingContext = false)
        => new HostBuilder().ConfigureServices(services =>
        {
            services.AddAuraRuntime();
            services.AddSingleton<ISettingsStore>(new FixedSettingsStore(settings));

            if (provider is not null) services.AddSingleton(provider);
            if (throwingContext) services.AddSingleton<IContextEngine, ThrowingContextEngine>();
            services.AddSingleton<IWin32Interop, TestInterop>();
            services.AddSingleton<ISurfaceView, TestSurfaceView>();
            services.AddSingleton<IClipboardArtifactManager, NullClipboardArtifactManager>();
            services.AddSingleton<SurfaceManager>();
            services.AddSingleton<ISurfaceManager>(sp => sp.GetRequiredService<SurfaceManager>());
            services.AddSingleton<Aura.UI.Interaction.IInteractionEngine, Aura.UI.Interaction.InteractionEngine>();
            services.Replace(ServiceDescriptor.Singleton<INotificationProjectionSink>(sp => sp.GetRequiredService<SurfaceManager>()));
        }).Build();

    private static AuraNotification CreateNotification(string id)
        => new(id, "Mail", id, "sensitive body", DateTimeOffset.Parse("2026-10-17T12:00:00Z"), Array.Empty<NotificationAction>());

    private static RuntimeEvaluationInput CreateInput(AuraNotification notification)
        => CreateInput(notification, authorized: false);

    private static RuntimeEvaluationInput CreateInput(AuraNotification notification, bool authorized, bool prioritySource = false)
        => new(new ContextInput(), new PriorityInput(notification.Id, IsPrioritySource: prioritySource),
            new PolicyRequest(PolicyResource.SensitiveData, "surface-notification", IsSensitive: true,
                AuthorizationKnown: true, IsAuthorized: authorized), notification);



    private sealed class NullClipboardArtifactManager : IClipboardArtifactManager
    {
        public ClipboardHistorySnapshot Current { get; } = new(Array.Empty<ClipboardArtifact>(), true, null);
        public event EventHandler<ClipboardHistorySnapshot>? Changed { add { } remove { } }
        public ValueTask StartAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask StopAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public IReadOnlyList<ClipboardArtifact> Query(string? filter = null) => Current.Items;
        public ValueTask<bool> CaptureAsync(ClipboardCaptureEvent capture, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public ValueTask<bool> RestoreAsync(string id, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public ValueTask<bool> SetPinnedAsync(string id, bool isPinned, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public ValueTask<bool> DeleteAsync(string id, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public void SetAvailability(bool isAvailable, string? status = null) { }
    }

    private sealed class FixedSettingsStore(AuraSettings current) : ISettingsStore
    {
        public AuraSettings Current { get; } = current;
        public event EventHandler<AuraSettings>? Changed
        {
            add { }
            remove { }
        }
        public ValueTask<AuraSettings> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(Current);
        public ValueTask SaveAsync(AuraSettings settings, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class FixedNotificationProvider(NotificationProviderResult result) : INotificationProvider
    {
        public ValueTask<NotificationProviderResult> GetNotificationsAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(result);
    }

    private sealed class TestSurfaceView : ISurfaceView
    {
        public UIElement Root => null!;
        public event EventHandler<InteractionIntentRequestedEventArgs>? InteractionRequested
        {
            add { }
            remove { }
        }
        public void Render(SurfacePresentationState state) { }
        public void RenderOnUiThread(SurfacePresentationState state, Action? afterRender = null) { Render(state); afterRender?.Invoke(); }
        public void BeginStartupEntrance() { }
    }

    private sealed class TestInterop : IWin32Interop
    {
        public void EnsurePerMonitorDpiAwareness() { }
        public uint GetDpiForWindow(nint windowHandle) => 96;
        public MonitorSnapshot GetMonitorSnapshot(nint windowHandle) => new("test", 0, 0, 1000, 800, 0, 0, 1000, 800, 96, true);
        public void SetWindowBounds(nint windowHandle, int left, int top, int width, int height) { }
        public void ConfigureFloatingWindow(nint windowHandle) { }
    }

    private sealed class ThrowingContextEngine : IContextEngine
    {
        public ContextSnapshot Current => ContextSnapshot.Unknown;
        public event EventHandler<ContextChangedEventArgs>? ContextChanged
        {
            add { }
            remove { }
        }
        public ContextSnapshot Evaluate(ContextInput input) => throw new InvalidOperationException("Test context failure.");
    }
}
