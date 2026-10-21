using Aura.Runtime;
using Aura.Runtime.Artifacts;
using Aura.Runtime.Settings;
using FluentAssertions;
using Aura.UI.Interaction;
using Aura.UI.Surface;

namespace Aura.Tests.Unit;

public sealed class ClipboardRetentionTests
{
    [Xunit.Fact]
    public async Task RetentionExpiresOnCaptureAndPinnedItemsSurviveUntilUnpinned()
    {
        var clock = new TestTimeProvider(DateTimeOffset.Parse("2026-10-20T12:00:00Z"));
        var old = new ClipboardArtifact("old", "old", clock.GetUtcNow().AddDays(-31), false, false, null);
        var pinned = new ClipboardArtifact("pinned", "pinned", clock.GetUtcNow().AddDays(-31), true, false, null);
        var persistence = new SeedPersistence([old, pinned]);
        var fixture = await Fixture.CreateAsync(new AuraSettings(RetentionDays: 30), persistence, clock);

        fixture.Manager.Query().Select(item => item.Id).Should().ContainSingle().Which.Should().Be("pinned");
        await fixture.PublishAsync("new", 1);
        fixture.Manager.Query().Select(item => item.Text).Should().ContainInOrder("new", "pinned");
        (await fixture.Manager.SetPinnedAsync("pinned", false)).Should().BeTrue();
        clock.Advance(TimeSpan.FromDays(31));
        fixture.Manager.Query().Should().BeEmpty();
    }

    [Xunit.Fact]
    public async Task RetentionZeroRemovesUnpinnedItemsDuringLoad()
    {
        var item = new ClipboardArtifact("expired", "text", DateTimeOffset.UtcNow, false, false, null);
        var fixture = await Fixture.CreateAsync(new AuraSettings(RetentionDays: 0), new SeedPersistence([item]), TimeProvider.System);
        fixture.Manager.Query().Should().BeEmpty();
        fixture.Persistence.Saved.Should().BeEmpty();
    }

    [Xunit.Fact]
    public async Task DisablingSensitiveSettingRemovesExistingSensitiveContentFromMemoryAndFile()
    {
        const string canary = "RETENTION-CANARY-11d2 password=notpersist";
        var settings = new MutableSettingsStore(new AuraSettings(AllowSensitiveData: true));
        var persistence = new SeedPersistence([]);
        var fixture = await Fixture.CreateAsync(settings, persistence, TimeProvider.System);
        await fixture.PublishAsync(canary, 2);
        fixture.Manager.Query().Should().ContainSingle();
        var machine = new StateMachine();
        var view = new RecordingView();
        using var surface = new SurfaceManager(view, new NullInterop(), Microsoft.Extensions.Logging.Abstractions.NullLogger<SurfaceManager>.Instance,
            machine, new InteractionEngine(machine, fixture.Manager), fixture.Manager);

        await settings.SetAsync(new AuraSettings(AllowSensitiveData: false));

        fixture.Manager.Query().Should().BeEmpty();
        persistence.Serialized.Should().NotContain("RETENTION-CANARY-11d2");
        surface.Current.ClipboardItems.Should().BeEmpty();
        view.LastState?.ClipboardItems.Should().BeEmpty();
    }

    private sealed class Fixture
    {
        private Fixture(ClipboardArtifactManager manager, EventDispatcher events, SeedPersistence persistence)
            => (Manager, Events, Persistence) = (manager, events, persistence);
        public ClipboardArtifactManager Manager { get; }
        public EventDispatcher Events { get; }
        public SeedPersistence Persistence { get; }
        public static async Task<Fixture> CreateAsync(AuraSettings settings, SeedPersistence? persistence = null, TimeProvider? clock = null)
            => await CreateAsync(new MutableSettingsStore(settings), persistence ?? new SeedPersistence([]), clock ?? TimeProvider.System);
        public static async Task<Fixture> CreateAsync(MutableSettingsStore settings, SeedPersistence persistence, TimeProvider clock)
        {
            var events = new EventDispatcher();
            var manager = new ClipboardArtifactManager(persistence, new Writer(), settings, events, new ClipboardSensitivityClassifier(), clock);
            await manager.StartAsync();
            return new Fixture(manager, events, persistence);
        }
        public ValueTask PublishAsync(string text, long sequence) => Events.PublishAsync(new ClipboardCaptureEvent(DateTimeOffset.UtcNow, text, null, sequence));
    }

    private sealed class SeedPersistence(IReadOnlyList<ClipboardArtifact> seed) : IClipboardArtifactPersistence
    {
        public IReadOnlyList<ClipboardArtifact> Saved { get; private set; } = seed;
        public string Serialized { get; private set; } = string.Empty;
        public ValueTask<IReadOnlyList<ClipboardArtifact>> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(Saved);
        public ValueTask SaveAsync(IReadOnlyList<ClipboardArtifact> items, CancellationToken cancellationToken = default)
        {
            Saved = items.ToArray();
            Serialized = System.Text.Json.JsonSerializer.Serialize(items);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class MutableSettingsStore(AuraSettings initial) : ISettingsStore
    {
        public AuraSettings Current { get; private set; } = initial;
        public event EventHandler<AuraSettings>? Changed;
        public ValueTask<AuraSettings> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(Current);
        public ValueTask SaveAsync(AuraSettings settings, CancellationToken cancellationToken = default) => SetAsync(settings);
        public ValueTask SetAsync(AuraSettings settings) { Current = settings; Changed?.Invoke(this, settings); return ValueTask.CompletedTask; }
    }

    private sealed class RecordingView : ISurfaceView
    {
        public Microsoft.UI.Xaml.UIElement Root => null!;
        public event EventHandler<InteractionIntentRequestedEventArgs>? InteractionRequested { add { } remove { } }
        public SurfacePresentationState? LastState { get; private set; }
        public void Render(SurfacePresentationState state) => LastState = state;
        public void RenderOnUiThread(SurfacePresentationState state, Action? afterRender = null) { Render(state); afterRender?.Invoke(); }
        public void BeginStartupEntrance() { }
    }

    private sealed class NullInterop : Aura.UI.Interop.IWin32Interop
    {
        public void EnsurePerMonitorDpiAwareness() { }
        public uint GetDpiForWindow(nint windowHandle) => 96;
        public Aura.UI.Interop.MonitorSnapshot GetMonitorSnapshot(nint windowHandle) => new("DISPLAY1", 0, 0, 1920, 1080, 0, 0, 1920, 1040, 96, true);
        public void SetWindowBounds(nint windowHandle, int left, int top, int width, int height) { }
        public void ConfigureFloatingWindow(nint windowHandle) { }
    }

    private sealed class Writer : IClipboardTextWriter
    {
        public bool TrySetText(string text, out long sequenceNumber) { sequenceNumber = 99; return true; }
    }

    private sealed class TestTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan amount) => _now += amount;
    }
}
