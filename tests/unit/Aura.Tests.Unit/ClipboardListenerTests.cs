using Aura.Runtime;
using Aura.Runtime.Artifacts;
using Aura.Runtime.Settings;
using Aura.UI.Interop;
using FluentAssertions;

namespace Aura.Tests.Unit;

public sealed class ClipboardListenerTests
{
    [Xunit.Fact]
    public void StartBeforeWindowAttachesExactlyOnceAndRepeatedStartIsIdempotent()
    {
        var fixture = Create();
        fixture.Listener.Start();
        fixture.Listener.Start();
        fixture.Listener.RegistrationCount.Should().Be(0);
        fixture.Listener.Attach((nint)42);
        fixture.Listener.Attach((nint)42);
        fixture.Listener.RegistrationCount.Should().Be(1);
        fixture.Native.AddCount.Should().Be(1);
    }

    [Xunit.Fact]
    public void StartAfterWindowRegistersAndStopIsIdempotentAndRestartRegistersAgain()
    {
        var fixture = Create();
        fixture.Listener.Attach((nint)43);
        fixture.Listener.Start();
        fixture.Listener.Start();
        fixture.Listener.RegistrationCount.Should().Be(1);
        fixture.Listener.Stop();
        fixture.Listener.Stop();
        fixture.Listener.RegistrationCount.Should().Be(0);
        fixture.Listener.Start();
        fixture.Listener.RegistrationCount.Should().Be(1);
        fixture.Native.AddCount.Should().Be(2);
        fixture.Native.RemoveCount.Should().Be(1);
    }

    [Xunit.Fact]
    public void AttachReplacementAndStopRemovePreviousRegistration()
    {
        var fixture = Create();
        fixture.Listener.Start();
        fixture.Listener.Attach((nint)44);
        fixture.Listener.Attach((nint)45);
        fixture.Listener.RegistrationCount.Should().Be(1);
        fixture.Native.RemoveCount.Should().Be(1);
        fixture.Listener.Stop();
        fixture.Native.RemoveCount.Should().Be(2);
    }

    [Xunit.Fact]
    public async Task LockedOrUnavailableClipboardFailsWithoutBlockingOrStoppingListener()
    {
        var fixture = Create();
        fixture.Native.ReadSuccess = false;
        fixture.Listener.Start();
        fixture.Listener.Attach((nint)46);
        var started = DateTime.UtcNow;
        fixture.Native.InvokeClipboardUpdate();
        (DateTime.UtcNow - started).Should().BeLessThan(TimeSpan.FromMilliseconds(100));
        await fixture.Native.WaitForReadsAsync();
        fixture.Listener.RegistrationCount.Should().Be(1);
        fixture.Manager.Current.IsAvailable.Should().BeFalse();
        fixture.Manager.Current.Items.Should().BeEmpty();
        fixture.Listener.Dispose();
    }

    [Xunit.Fact]
    public async Task BurstNotificationsAreBoundedSerializedAndCaptureFinalStateWithoutDuplicates()
    {
        var events = new EventDispatcher();
        var manager = new ClipboardArtifactManager(new FakePersistence(), new FakeWriter(), new FakeSettingsStore(), events,
            new ClipboardSensitivityClassifier(), TimeProvider.System);
        await manager.StartAsync();
        var native = new FakeNative { CurrentText = "initial", CurrentSequence = 1, ReadDelay = TimeSpan.FromMilliseconds(20) };
        var listener = new ClipboardWindowListener(native, events, manager);
        listener.Start();
        listener.Attach((nint)47);
        for (var index = 2; index < 502; index++)
        {
            native.CurrentText = index < 300 ? "repeated" : "final";
            native.CurrentSequence = index;
            native.InvokeClipboardUpdate();
        }

        await native.WaitForReadsAsync();
        native.MaxConcurrentReads.Should().Be(1);
        manager.Query().Should().ContainSingle().Which.Text.Should().Be("final");
        listener.RegistrationCount.Should().Be(1);
        listener.Stop();
        listener.RegistrationCount.Should().Be(0);
        listener.Dispose();
        await manager.StopAsync();
        manager.Dispose();
    }

    private static (ClipboardWindowListener Listener, FakeNative Native, TestArtifactManager Manager) Create()
    {
        var events = new EventDispatcher();
        var manager = new TestArtifactManager();
        var native = new FakeNative();
        return (new ClipboardWindowListener(native, events, manager), native, manager);
    }

    private sealed class FakeNative : IClipboardNativeApi
    {
        private ClipboardSubclassProcedure? _procedure;
        private readonly ManualResetEventSlim _readObserved = new();
        private int _activeReads;
        public int AddCount { get; private set; }
        public int RemoveCount { get; private set; }
        public bool ReadSuccess { get; set; } = true;
        public string CurrentText { get; set; } = "copied";
        public long CurrentSequence { get; set; } = 1;
        public TimeSpan ReadDelay { get; set; }
        public int MaxConcurrentReads { get; private set; }
        public bool AddListener(nint windowHandle) { AddCount++; return true; }
        public bool RemoveListener(nint windowHandle) { RemoveCount++; return true; }
        public bool InstallSubclass(nint windowHandle, ClipboardSubclassProcedure procedure, nuint subclassId) { _procedure = procedure; return true; }
        public bool RemoveSubclass(nint windowHandle, ClipboardSubclassProcedure procedure, nuint subclassId) { _procedure = null; return true; }
        public bool TryReadText(nint windowHandle, out string? text, out long sequenceNumber)
        {
            var active = Interlocked.Increment(ref _activeReads);
            MaxConcurrentReads = Math.Max(MaxConcurrentReads, active);
            try
            {
                if (ReadDelay > TimeSpan.Zero) Thread.Sleep(ReadDelay);
                text = ReadSuccess ? CurrentText : null;
                sequenceNumber = CurrentSequence;
                _readObserved.Set();
                return ReadSuccess;
            }
            finally { Interlocked.Decrement(ref _activeReads); }
        }
        public long GetClipboardSequenceNumber() => CurrentSequence;
        public bool TrySetText(string text, out long sequenceNumber) { sequenceNumber = 1; return true; }
        public nint ForwardMessage(nint windowHandle, uint message, nint wParam, nint lParam) => 0;
        public void InvokeClipboardUpdate() => _procedure?.Invoke((nint)46, 0x031D, 0, 0, 1, 0);
        public async Task WaitForReadsAsync()
        {
            await Task.Run(() => _readObserved.Wait(TimeSpan.FromSeconds(3)));
            while (_activeReads != 0) await Task.Delay(10);
            await Task.Delay(100);
        }
    }

    private sealed class TestArtifactManager : IClipboardArtifactManager
    {
        public ClipboardHistorySnapshot Current { get; private set; } = new(Array.Empty<ClipboardArtifact>(), true, null);
        public event EventHandler<ClipboardHistorySnapshot>? Changed { add { } remove { } }
        public ValueTask StartAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask StopAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public IReadOnlyList<ClipboardArtifact> Query(string? filter = null) => Current.Items;
        public ValueTask<bool> CaptureAsync(ClipboardCaptureEvent capture, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public ValueTask<bool> RestoreAsync(string id, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public ValueTask<bool> SetPinnedAsync(string id, bool isPinned, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public ValueTask<bool> DeleteAsync(string id, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public void SetAvailability(bool isAvailable, string? status = null) => Current = Current with { IsAvailable = isAvailable, Status = status };
    }

    private sealed class FakePersistence : IClipboardArtifactPersistence
    {
        private IReadOnlyList<ClipboardArtifact> _items = Array.Empty<ClipboardArtifact>();
        public ValueTask<IReadOnlyList<ClipboardArtifact>> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(_items);
        public ValueTask SaveAsync(IReadOnlyList<ClipboardArtifact> items, CancellationToken cancellationToken = default)
        {
            _items = items.ToArray();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeWriter : IClipboardTextWriter
    {
        public bool TrySetText(string text, out long sequenceNumber) { sequenceNumber = 0; return true; }
    }

    private sealed class FakeSettingsStore : ISettingsStore
    {
        public AuraSettings Current { get; } = new();
        public event EventHandler<AuraSettings>? Changed { add { } remove { } }
        public ValueTask<AuraSettings> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(Current);
        public ValueTask SaveAsync(AuraSettings settings, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
