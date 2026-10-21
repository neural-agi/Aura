using Aura.Runtime;
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
        var engine = new InteractionEngine(machine, new NullClipboardArtifactManager());

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

        (await new InteractionEngine(machine, new NullClipboardArtifactManager()).RequestAsync(intent)).Should().BeTrue();
        machine.Current.Should().Be(expected);
    }

    [Xunit.Fact]
    public void SurfaceManagerProjectsAuthoritativeState()
    {
        var machine = new StateMachine();
        var view = new FakeSurfaceView();
        var artifacts = new NullClipboardArtifactManager();
        var manager = new SurfaceManager(view, new FakeInterop(), NullLogger<SurfaceManager>.Instance, machine, new InteractionEngine(machine, artifacts), artifacts);

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
        _ = new SurfaceManager(view, new FakeInterop(), NullLogger<SurfaceManager>.Instance, machine, new InteractionEngine(machine, artifacts), artifacts);
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
        var manager = new SurfaceManager(view, new FakeInterop(), NullLogger<SurfaceManager>.Instance, machine, new InteractionEngine(machine, artifacts), artifacts);
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
        public event EventHandler<InteractionIntentRequestedEventArgs>? InteractionRequested;
        public void Render(SurfacePresentationState state) { States.Add(state.Mode); LastState = state; }
        public void RenderOnUiThread(SurfacePresentationState state, Action? afterRender = null) { Render(state); afterRender?.Invoke(); }
        public void BeginStartupEntrance() { }

        public void Raise(InteractionIntent intent) => InteractionRequested?.Invoke(this, new(intent));
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
