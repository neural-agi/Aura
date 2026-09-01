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
    public void InteractionEngineMapsRecoverToRecoveredThenCompact()
    {
        var machine = new StateMachine();
        machine.TryTransition(AuraState.Surfacing);
        machine.TryTransition(AuraState.Compact);
        machine.TryTransition(AuraState.Dismissed);
        var engine = new InteractionEngine(machine);

        engine.Request(InteractionIntent.Recover).Should().BeTrue();
        machine.Current.Should().Be(AuraState.Compact);
    }

    [Xunit.Theory]
    [Xunit.InlineData(InteractionIntent.Expand, AuraState.Expanded)]
    [Xunit.InlineData(InteractionIntent.Collapse, AuraState.Compact)]
    [Xunit.InlineData(InteractionIntent.EnterInteractive, AuraState.Interactive)]
    [Xunit.InlineData(InteractionIntent.Dismiss, AuraState.Dismissed)]
    public void InteractionEngineMapsIntentToAuthoritativeState(InteractionIntent intent, AuraState expected)
    {
        var machine = new StateMachine();
        machine.TryTransition(AuraState.Surfacing);
        machine.TryTransition(AuraState.Compact);
        if (intent == InteractionIntent.Collapse)
        {
            machine.TryTransition(AuraState.Expanded);
        }

        new InteractionEngine(machine).Request(intent).Should().BeTrue();
        machine.Current.Should().Be(expected);
    }

    [Xunit.Fact]
    public void SurfaceManagerProjectsAuthoritativeState()
    {
        var machine = new StateMachine();
        var view = new FakeSurfaceView();
        var manager = new SurfaceManager(view, new FakeInterop(), NullLogger<SurfaceManager>.Instance, machine, new InteractionEngine(machine));

        manager.Attach((nint)42);
        machine.TryTransition(AuraState.Surfacing);
        machine.TryTransition(AuraState.Compact);
        machine.TryTransition(AuraState.Expanded);

        manager.Current.Mode.Should().Be(SurfacePresentationMode.Expanded);
        view.States.Should().ContainInOrder(SurfacePresentationMode.Eligible, SurfacePresentationMode.Compact, SurfacePresentationMode.Expanded);
    }

    [Xunit.Fact]
    public void ViewIntentFlowsThroughInteractionEngineIntoProjection()
    {
        var machine = new StateMachine();
        var view = new FakeSurfaceView();
        _ = new SurfaceManager(view, new FakeInterop(), NullLogger<SurfaceManager>.Instance, machine, new InteractionEngine(machine));
        machine.TryTransition(AuraState.Surfacing);
        machine.TryTransition(AuraState.Compact);

        view.Raise(InteractionIntent.Expand);

        machine.Current.Should().Be(AuraState.Expanded);
        view.States[^1].Should().Be(SurfacePresentationMode.Expanded);
    }

    private sealed class FakeSurfaceView : ISurfaceView
    {
        public UIElement Root => null!;
        public List<SurfacePresentationMode> States { get; } = new();
        public event EventHandler<InteractionIntentRequestedEventArgs>? InteractionRequested;
        public void Render(SurfacePresentationState state) => States.Add(state.Mode);

        public void Raise(InteractionIntent intent) => InteractionRequested?.Invoke(this, new(intent));
    }

    private sealed class FakeInterop : IWin32Interop
    {
        public void EnsurePerMonitorDpiAwareness() { }
        public uint GetDpiForWindow(nint windowHandle) => 96;
        public MonitorSnapshot GetMonitorSnapshot(nint windowHandle) => new("DISPLAY1", 0, 0, 1920, 1080, 0, 0, 1920, 1040, 96, true);
        public void SetWindowBounds(nint windowHandle, int left, int top, int width, int height) { }
    }
}
