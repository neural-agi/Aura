using Aura.Runtime;
using Aura.Runtime.Media;
using Aura.UI.Interaction;
using Aura.UI.Interop;
using Aura.UI.Surface;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.UI.Xaml;

namespace Aura.Tests.Unit;

public sealed class SurfaceManagerTests
{
    [Xunit.Fact]
    public void StateMachineRejectsIllegalTransitionsAndPublishesValidOnes()
    {
        var machine = new StateMachine();
        var observed = new List<AuraState>();
        machine.StateChanged += (_, e) => observed.Add(e.Current);

        machine.TryTransition(AuraState.Expanded).Should().BeFalse();
        machine.TryTransition(AuraState.Surfacing).Should().BeTrue();
        machine.TryTransition(AuraState.Compact).Should().BeTrue();
        machine.TryTransition(AuraState.Expanded).Should().BeTrue();
        machine.TryTransition(AuraState.Dismissed).Should().BeTrue();
        machine.TryTransition(AuraState.Compact).Should().BeFalse();
        observed.Should().ContainInOrder(AuraState.Surfacing, AuraState.Compact, AuraState.Expanded, AuraState.Dismissed);
    }

    [Xunit.Fact]
    public async Task InteractionEngineMapsRecoverToRecoveredThenCompact()
    {
        var machine = new StateMachine();
        machine.TryTransition(AuraState.Surfacing);
        machine.TryTransition(AuraState.Compact);
        machine.TryTransition(AuraState.Dismissed);
        var engine = new InteractionEngine(machine, new NullClipboardArtifactManager(), new TestMediaService());

        (await engine.RequestAsync(InteractionIntent.Recover)).Should().BeTrue();
        machine.Current.Should().Be(AuraState.Compact);
    }

    [Xunit.Theory]
    [Xunit.InlineData(InteractionIntent.Expand, AuraState.Expanded)]
    [Xunit.InlineData(InteractionIntent.Collapse, AuraState.Compact)]
    [Xunit.InlineData(InteractionIntent.EnterInteractive, AuraState.Interactive)]
    [Xunit.InlineData(InteractionIntent.Dismiss, AuraState.Dismissed)]
    public async Task InteractionEngineMapsIntentToAuthoritativeState(InteractionIntent intent, AuraState expected)
    {
        var machine = new StateMachine();
        machine.TryTransition(AuraState.Surfacing);
        machine.TryTransition(AuraState.Compact);
        if (intent == InteractionIntent.Collapse)
        {
            machine.TryTransition(AuraState.Expanded);
        }

        (await new InteractionEngine(machine, new NullClipboardArtifactManager(), new TestMediaService()).RequestAsync(intent)).Should().BeTrue();
        machine.Current.Should().Be(expected);
    }

    [Xunit.Fact]
    public void SurfaceManagerProjectsAuthoritativeState()
    {
        var machine = new StateMachine();
        var view = new FakeSurfaceView();
        var artifacts = new NullClipboardArtifactManager();
        var media = new TestMediaService();
        var manager = new SurfaceManager(view, new FakeInterop(), NullLogger<SurfaceManager>.Instance, machine, new InteractionEngine(machine, artifacts, media), artifacts, media);

        manager.Attach((nint)42);
        machine.TryTransition(AuraState.Surfacing);
        machine.TryTransition(AuraState.Compact);
        machine.TryTransition(AuraState.Expanded);

        manager.Current.Mode.Should().Be(SurfacePresentationMode.Expanded);
        view.States.Should().ContainInOrder(SurfacePresentationMode.Eligible, SurfacePresentationMode.Compact, SurfacePresentationMode.Expanded);
    }

    [Xunit.Fact]
    public void DismissedProjectionHasNoDismissedStatusText()
    {
        var machine = new StateMachine();
        var view = new FakeSurfaceView();
        var artifacts = new NullClipboardArtifactManager();
        var media = new TestMediaService();
        _ = new SurfaceManager(view, new FakeInterop(), NullLogger<SurfaceManager>.Instance, machine, new InteractionEngine(machine, artifacts, media), artifacts, media);
        machine.TryTransition(AuraState.Surfacing);
        machine.TryTransition(AuraState.Compact);
        machine.TryTransition(AuraState.Dismissed);

        view.LastState!.Mode.Should().Be(SurfacePresentationMode.Dismissed);
        view.LastState.StatusText.Should().BeEmpty();
        view.LastState.IsVisible.Should().BeFalse();
    }

    [Xunit.Fact]
    public async Task ClipboardHistoryProjectionRemainsOpenAcrossStateProjectionAndDismissalClosesIt()
    {
        var machine = new StateMachine();
        var view = new FakeSurfaceView();
        var artifacts = new NullClipboardArtifactManager();
        var media = new TestMediaService();
        var manager = new SurfaceManager(view, new FakeInterop(), NullLogger<SurfaceManager>.Instance, machine, new InteractionEngine(machine, artifacts, media), artifacts, media);
        machine.TryTransition(AuraState.Surfacing);
        machine.TryTransition(AuraState.Compact);

        view.Raise(InteractionIntent.BrowseClipboard);
        await Task.Delay(25);
        machine.Current.Should().Be(AuraState.Interactive);
        manager.Current.IsClipboardHistoryOpen.Should().BeTrue();
        manager.Current.Mode.Should().Be(SurfacePresentationMode.Interactive);

        machine.TryTransition(AuraState.Dismissed);
        manager.Current.IsClipboardHistoryOpen.Should().BeFalse();
        manager.Current.Mode.Should().Be(SurfacePresentationMode.Dismissed);
    }

    [Xunit.Fact]
    public async Task MediaIntentFlowsThroughInteractionEngineWithoutChangingAuraState()
    {
        var machine = new StateMachine();
        machine.TryTransition(AuraState.Surfacing);
        machine.TryTransition(AuraState.Compact);
        var view = new FakeSurfaceView();
        var artifacts = new NullClipboardArtifactManager();
        var media = new RecordingMediaService();
        _ = new SurfaceManager(view, new FakeInterop(), NullLogger<SurfaceManager>.Instance, machine,
            new InteractionEngine(machine, artifacts, media), artifacts, media);

        view.Raise(InteractionIntent.MediaPrevious);
        await Task.Yield();

        media.Commands.Should().ContainSingle().Which.Should().Be(MediaCommand.Previous);
        machine.Current.Should().Be(AuraState.Compact);
    }

    private sealed class RecordingMediaService : IMediaControlService
    {
        public MediaProjection Current { get; } = MediaProjection.Unavailable with
        {
            Session = new MediaSessionSnapshot("session", "source", "title", "artist", null, null, null, MediaPlaybackState.Playing, true, true, true)
        };
        public List<MediaCommand> Commands { get; } = new();
        public event EventHandler<MediaProjection>? Changed { add { } remove { } }
        public ValueTask StartAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask StopAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask<MediaCommandResult> ExecuteAsync(MediaCommand command, CancellationToken cancellationToken = default)
        {
            Commands.Add(command);
            return ValueTask.FromResult(MediaCommandResult.Succeeded);
        }
        public ValueTask<bool> SetVolumeAsync(double level, CancellationToken cancellationToken = default) => ValueTask.FromResult(true);
    }

    [Xunit.Fact]
    public async Task MediaHomeNavigationClearsOnlyProjectionAndReentryUsesCurrentSession()
    {
        var machine = new StateMachine();
        machine.TryTransition(AuraState.Surfacing);
        machine.TryTransition(AuraState.Compact);
        var view = new FakeSurfaceView();
        var artifacts = new NullClipboardArtifactManager();
        var media = new MutableMediaService
        {
            Projection = ActiveProjection("session-a", "First title", [1, 2, 3])
        };
        var engine = new InteractionEngine(machine, artifacts, media);
        var manager = new SurfaceManager(view, new FakeInterop(), NullLogger<SurfaceManager>.Instance, machine, engine, artifacts, media);

        view.Raise(InteractionIntent.OpenMedia);
        await view.WaitForRenderAsync();
        manager.Current.Media!.Session.Title.Should().Be("First title");
        machine.Current.Should().Be(AuraState.Interactive);

        view.Raise(InteractionIntent.Collapse);
        await view.WaitForRenderAsync();
        manager.Current.Mode.Should().Be(SurfacePresentationMode.Compact);
        manager.Current.IsVisible.Should().BeTrue();
        manager.Current.Media.Should().BeNull();
        view.LastState!.IsMediaSurfaceOpen.Should().BeFalse();
        view.LastState.Mode.Should().Be(SurfacePresentationMode.Compact);
        machine.Current.Should().Be(AuraState.Compact);
        media.Commands.Should().BeEmpty();
        media.VolumeWrites.Should().Be(0);

        media.Projection = ActiveProjection("session-b", "Current title", [9, 8]);
        media.RaiseChanged();
        view.Raise(InteractionIntent.OpenMedia);
        await view.WaitForRenderAsync();
        manager.Current.Media!.Session.Title.Should().Be("Current title");
        manager.Current.Media.Session.Artwork.Should().Equal(9, 8);
        machine.Current.Should().Be(AuraState.Interactive);
    }

    [Xunit.Fact]
    public async Task MediaDisappearanceOnHomeShowsNoSessionWhenReopenedAndCyclesDoNotAccumulateProjection()
    {
        var machine = new StateMachine();
        machine.TryTransition(AuraState.Surfacing);
        machine.TryTransition(AuraState.Compact);
        var view = new FakeSurfaceView();
        var artifacts = new NullClipboardArtifactManager();
        var media = new MutableMediaService { Projection = ActiveProjection("session-a", "Title", [1]) };
        var manager = new SurfaceManager(view, new FakeInterop(), NullLogger<SurfaceManager>.Instance, machine,
            new InteractionEngine(machine, artifacts, media), artifacts, media);

        for (var cycle = 0; cycle < 3; cycle++)
        {
            view.Raise(InteractionIntent.OpenMedia);
            await view.WaitForRenderAsync();
            manager.Current.Media!.Session.SessionId.Should().Be("session-a");
            view.Raise(InteractionIntent.Collapse);
            await view.WaitForRenderAsync();
            manager.Current.Media.Should().BeNull();
            machine.Current.Should().Be(AuraState.Compact);
        }

        media.Projection = MediaProjection.Unavailable;
        media.RaiseChanged();
        manager.Current.Mode.Should().Be(SurfacePresentationMode.Compact);
        view.Raise(InteractionIntent.OpenMedia);
        await view.WaitForRenderAsync();
        manager.Current.Media!.Session.Title.Should().BeNull();
        manager.Current.Media.Session.Artwork.Should().BeNull();
        media.EventSubscribers.Should().Be(1);
        media.Commands.Should().BeEmpty();
        media.VolumeWrites.Should().Be(0);
    }

    [Xunit.Fact]
    public void MediaChangesWhileHomeDoNotChangeSurfaceState()
    {
        var machine = new StateMachine();
        machine.TryTransition(AuraState.Surfacing);
        machine.TryTransition(AuraState.Compact);
        var view = new FakeSurfaceView();
        var artifacts = new NullClipboardArtifactManager();
        var media = new MutableMediaService { Projection = MediaProjection.Unavailable };
        var manager = new SurfaceManager(view, new FakeInterop(), NullLogger<SurfaceManager>.Instance, machine,
            new InteractionEngine(machine, artifacts, media), artifacts, media);
        var initial = manager.Current;

        media.Projection = ActiveProjection("session-a", "Track one", [1]);
        media.RaiseChanged();
        media.Projection = ActiveProjection("session-a", "Track two", [2]);
        media.RaiseChanged();
        media.Projection = ActiveProjection("session-a", "Track two", [2], MediaPlaybackState.Paused);
        media.RaiseChanged();

        manager.Current.Mode.Should().Be(initial.Mode);
        manager.Current.IsMediaSurfaceOpen.Should().BeFalse();
        manager.Current.IsVisible.Should().Be(initial.IsVisible);
        machine.Current.Should().Be(AuraState.Compact);
        view.Raise(InteractionIntent.OpenMedia);
        manager.Current.IsMediaSurfaceOpen.Should().BeTrue();
    }

    [Xunit.Fact]
    public void VolumePresentationUsesInvariantNumericValueAndClampsKnownValues()
    {
        MediaPresentationFormatting.FormatVolumeValue(0d).Should().Be("0");
        MediaPresentationFormatting.FormatVolumeValue(0.37d).Should().Be("37");
        MediaPresentationFormatting.FormatVolumeValue(0.50d).Should().Be("50");
        MediaPresentationFormatting.FormatVolumeValue(1d).Should().Be("100");
        MediaPresentationFormatting.FormatVolumeValue(0.375d).Should().Be("38");
        MediaPresentationFormatting.FormatVolumeValue(0.995d).Should().Be("100");
        MediaPresentationFormatting.FormatVolumeValue(-1d).Should().Be("0");
        MediaPresentationFormatting.FormatVolumeValue(2d).Should().Be("100");
        MediaPresentationFormatting.FormatVolumeValue(double.NaN).Should().Be("Unavailable");
        MediaPresentationFormatting.FormatVolumeValue(null).Should().Be("Unavailable");
        MediaPresentationFormatting.FormatSourceName("Spotify.exe").Should().Be("Spotify");
        MediaPresentationFormatting.FormatSourceName("Package.Family!Player").Should().Be("Player");
        MediaPresentationFormatting.FormatSourceName("unfamiliar-id").Should().Be("unfamiliar-id");
    }

    private static MediaProjection ActiveProjection(string id, string title, byte[] artwork, MediaPlaybackState playback = MediaPlaybackState.Playing)
        => new(new MediaSessionSnapshot(id, "Spotify.exe", title, "Artist", "Album", artwork, "image/png", playback, true, true, true), new MediaVolumeSnapshot(true, 0.37));

    [Xunit.Fact]
    public void ViewIntentFlowsThroughInteractionEngineIntoProjection()
    {
        var machine = new StateMachine();
        var view = new FakeSurfaceView();
        var artifacts = new NullClipboardArtifactManager();
        _ = new SurfaceManager(view, new FakeInterop(), NullLogger<SurfaceManager>.Instance, machine, new InteractionEngine(machine, artifacts), artifacts);
        machine.TryTransition(AuraState.Surfacing);
        machine.TryTransition(AuraState.Compact);

        view.Raise(InteractionIntent.Expand);

        machine.Current.Should().Be(AuraState.Expanded);
        view.States[^1].Should().Be(SurfacePresentationMode.Expanded);
    }

    private sealed class MutableMediaService : IMediaControlService
    {
        private EventHandler<MediaProjection>? _changed;
        public MediaProjection Projection { get; set; } = MediaProjection.Unavailable;
        public MediaProjection Current => Projection;
        public int EventSubscribers => _changed?.GetInvocationList().Length ?? 0;
        public int VolumeWrites { get; private set; }
        public List<MediaCommand> Commands { get; } = new();
        public event EventHandler<MediaProjection>? Changed { add => _changed += value; remove => _changed -= value; }
        public ValueTask StartAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask StopAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask<MediaCommandResult> ExecuteAsync(MediaCommand command, CancellationToken cancellationToken = default)
        {
            Commands.Add(command);
            return ValueTask.FromResult(MediaCommandResult.Succeeded);
        }
        public ValueTask<bool> SetVolumeAsync(double level, CancellationToken cancellationToken = default)
        {
            VolumeWrites++;
            return ValueTask.FromResult(true);
        }
        public void RaiseChanged() => _changed?.Invoke(this, Projection);
    }

    private sealed class TestMediaService : IMediaControlService
    {
        public MediaProjection Current { get; private set; } = MediaProjection.Unavailable;
        public event EventHandler<MediaProjection>? Changed { add { } remove { } }
        public ValueTask StartAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask StopAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask<MediaCommandResult> ExecuteAsync(MediaCommand command, CancellationToken cancellationToken = default) => ValueTask.FromResult(MediaCommandResult.Succeeded);
        public ValueTask<bool> SetVolumeAsync(double level, CancellationToken cancellationToken = default) => ValueTask.FromResult(true);
    }

    private sealed class NullClipboardArtifactManager : Aura.Runtime.Artifacts.IClipboardArtifactManager
    {
        public Aura.Runtime.Artifacts.ClipboardHistorySnapshot Current { get; } = new(Array.Empty<Aura.Runtime.Artifacts.ClipboardArtifact>(), true, null);
        public event EventHandler<Aura.Runtime.Artifacts.ClipboardHistorySnapshot>? Changed { add { } remove { } }
        public ValueTask StartAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask StopAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public IReadOnlyList<Aura.Runtime.Artifacts.ClipboardArtifact> Query(string? filter = null) => Current.Items;
        public ValueTask<bool> CaptureAsync(Aura.Runtime.Artifacts.ClipboardCaptureEvent capture, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public ValueTask<bool> RestoreAsync(string id, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public ValueTask<bool> SetPinnedAsync(string id, bool isPinned, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public ValueTask<bool> DeleteAsync(string id, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public void SetAvailability(bool isAvailable, string? status = null) { }
    }

    private sealed class FakeSurfaceView : ISurfaceView
    {
        public UIElement Root => null!;
        public List<SurfacePresentationMode> States { get; } = new();
        public SurfacePresentationState? LastState { get; private set; }
        private TaskCompletionSource _rendered = NewSignal();
        public event EventHandler<InteractionIntentRequestedEventArgs>? InteractionRequested;
        public void Render(SurfacePresentationState state)
        {
            States.Add(state.Mode);
            LastState = state;
            _rendered.TrySetResult();
        }
        public void RenderOnUiThread(SurfacePresentationState state, Action? afterRender = null) { Render(state); afterRender?.Invoke(); }
        public void BeginStartupEntrance() { }

        public void Raise(InteractionIntent intent)
        {
            _rendered = NewSignal();
            InteractionRequested?.Invoke(this, new(intent));
        }

        public Task WaitForRenderAsync() => _rendered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class FakeInterop : IWin32Interop
    {
        public void EnsurePerMonitorDpiAwareness() { }
        public uint GetDpiForWindow(nint windowHandle) => 96;
        public MonitorSnapshot GetMonitorSnapshot(nint windowHandle) => new("DISPLAY1", 0, 0, 1920, 1080, 0, 0, 1920, 1040, 96, true);
        public void SetWindowBounds(nint windowHandle, int left, int top, int width, int height) { }
        public void ConfigureFloatingWindow(nint windowHandle) { }
    }
}
