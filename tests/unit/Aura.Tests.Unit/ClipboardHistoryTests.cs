using Aura.Runtime;
using Aura.Runtime.Artifacts;
using Aura.Runtime.Settings;
using FluentAssertions;
using Aura.Runtime.Context;
using Aura.Runtime.Decision;
using Aura.Runtime.Notifications;
using Aura.Runtime.Policy;
using Aura.Runtime.Priority;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aura.Tests.Unit;

public sealed class ClipboardHistoryTests
{
    [Xunit.Fact]
    public async Task CapturesRepeatedUnicodeCopiesNewestFirstAndFilters()
    {
        var fixture = await Fixture.CreateAsync(new AuraSettings(RetentionDays: 30));
        await fixture.PublishAsync("older Ω text", 1);
        await fixture.PublishAsync("newer 日本語 text", 2);
        await fixture.PublishAsync("different final text", 3);

        fixture.Manager.Query().Select(item => item.Text).Should().ContainInOrder("different final text", "newer 日本語 text", "older Ω text");
        fixture.Manager.Query("日本語").Should().ContainSingle();
    }

    [Xunit.Fact]
    public async Task ConsecutiveCopiesCoalesceButAThenBThenARemainSeparate()
    {
        var fixture = await Fixture.CreateAsync(new AuraSettings());
        await fixture.PublishAsync("A", DateTimeOffset.UnixEpoch.AddTicks(1));
        var firstId = fixture.Manager.Query().Single().Id;
        await fixture.PublishAsync("A", DateTimeOffset.UnixEpoch.AddTicks(2));
        await fixture.PublishAsync("A", DateTimeOffset.UnixEpoch.AddTicks(3));
        fixture.Manager.Query().Should().ContainSingle().Which.Id.Should().Be(firstId);
        fixture.Manager.Query().Single().CapturedAtUtc.Should().Be(DateTimeOffset.UnixEpoch.AddTicks(3));

        await fixture.PublishAsync("B", DateTimeOffset.UnixEpoch.AddTicks(4));
        await fixture.PublishAsync("A", DateTimeOffset.UnixEpoch.AddTicks(5));
        fixture.Manager.Query().Select(item => item.Text).Should().ContainInOrder("A", "B", "A");
        fixture.Manager.Query().Select(item => item.Id).Distinct().Should().HaveCount(3);
    }

    [Xunit.Fact]
    public async Task CoalescingPreservesPinnedIdentityAndSurvivesRestart()
    {
        var persistence = new MemoryPersistence();
        var fixture = await Fixture.CreateAsync(new AuraSettings(), persistence);
        await fixture.PublishAsync("pinned repeated", DateTimeOffset.UnixEpoch.AddTicks(10));
        var id = fixture.Manager.Query().Single().Id;
        await fixture.Manager.SetPinnedAsync(id, true);
        await fixture.PublishAsync("pinned repeated", DateTimeOffset.UnixEpoch.AddTicks(11));
        fixture.Manager.Query().Should().ContainSingle().Which.Should().Match<ClipboardArtifact>(item => item.Id == id && item.IsPinned);

        var restored = await Fixture.CreateAsync(new AuraSettings(), persistence);
        restored.Manager.Query().Should().ContainSingle().Which.Should().Match<ClipboardArtifact>(item => item.Id == id && item.IsPinned);
    }

    [Xunit.Fact]
    public async Task SensitiveCanaryIsNeitherPersistedNorQueryableWhenDisabled()
    {
        const string canary = "CLIPBOARD-CANARY-6af51d Password=secret-value";
        var persistence = new MemoryPersistence();
        var fixture = await Fixture.CreateAsync(new AuraSettings(AllowSensitiveData: false), persistence);
        await fixture.PublishAsync(canary, 20);

        persistence.Serialized.Should().NotContain("CLIPBOARD-CANARY-6af51d");
        fixture.Manager.Query(canary).Should().BeEmpty();
        fixture.Manager.Current.Items.Should().BeEmpty();
        string.Join("\n", fixture.Logs).Should().NotContain("CLIPBOARD-CANARY-6af51d");
    }

    [Xunit.Fact]
    public async Task SensitiveCanaryIsHandledWhenExplicitlyAllowed()
    {
        const string canary = "CLIPBOARD-CANARY-allowed Password=private";
        var persistence = new MemoryPersistence();
        var fixture = await Fixture.CreateAsync(new AuraSettings(AllowSensitiveData: true), persistence);
        await fixture.PublishAsync(canary, 21);

        fixture.Manager.Query(canary).Should().ContainSingle().Which.IsSensitive.Should().BeTrue();
        persistence.Serialized.Should().Contain("CLIPBOARD-CANARY-allowed");
        string.Join("\n", fixture.Logs).Should().NotContain("CLIPBOARD-CANARY-allowed");
    }

    [Xunit.Fact]
    public async Task RestoreSuppressionIsReservedBeforeWriteAndLaterSameTextCopyIsCaptured()
    {
        var fixture = await Fixture.CreateAsync(new AuraSettings());
        await fixture.PublishAsync("same text", 1);
        var first = fixture.Manager.Query().Single();
        fixture.Writer.OnSetText = (text, sequence) =>
            fixture.Manager.CaptureAsync(new ClipboardCaptureEvent(DateTimeOffset.UtcNow, text, null, sequence)).AsTask().GetAwaiter().GetResult();

        (await fixture.Manager.RestoreAsync(first.Id)).Should().BeTrue();
        fixture.Manager.Query().Should().ContainSingle();
        await fixture.Events.PublishAsync(new ClipboardCaptureEvent(DateTimeOffset.UtcNow, "different text", null, fixture.Writer.NextSequence + 1));
        fixture.Manager.Query().Should().HaveCount(2);
    }

    [Xunit.Fact]
    public async Task RestoreRunsClipboardWriterOffCallingThreadAndKeepsReservationDuringWrite()
    {
        var fixture = await Fixture.CreateAsync(new AuraSettings());
        await fixture.PublishAsync("restore without blocking UI", 4);
        var artifact = fixture.Manager.Query().Single();
        var callerStarted = new ManualResetEventSlim();
        var callerThreadId = 0;
        var writerStarted = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowWriteToFinish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Writer.BeforeSetText = () =>
        {
            writerStarted.TrySetResult(Environment.CurrentManagedThreadId);
            return allowWriteToFinish.Task;
        };
        var restore = Task.Factory.StartNew(async () =>
        {
            callerThreadId = Environment.CurrentManagedThreadId;
            callerStarted.Set();
            return await fixture.Manager.RestoreAsync(artifact.Id);
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
        callerStarted.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();

        var callerThread = callerThreadId;
        var writerThread = await writerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        writerThread.Should().NotBe(callerThread);
        fixture.Writer.LastWriterThreadId.Should().Be(writerThread);
        restore.IsCompleted.Should().BeFalse();
        (await fixture.Manager.CaptureAsync(new ClipboardCaptureEvent(DateTimeOffset.UtcNow, artifact.Text, null, fixture.Writer.NextSequence))).Should().BeFalse();

        allowWriteToFinish.TrySetResult();
        (await restore).Should().BeTrue();
        fixture.Manager.Query().Should().ContainSingle();
    }

    [Xunit.Fact]
    public async Task FilteringReadsOnlyInMemorySnapshotAndNeverPersists()
    {
        var persistence = new CountingPersistence();
        var fixture = await Fixture.CreateAsync(new AuraSettings(), persistence);
        await fixture.PublishAsync("alpha clipboard", 5);
        await fixture.PublishAsync("beta clipboard", 6);
        persistence.LoadCount.Should().Be(1);
        persistence.ResetCounts();

        fixture.Manager.Query("alpha").Select(item => item.Text).Should().ContainSingle().Which.Should().Be("alpha clipboard");
        fixture.Manager.Query("clipboard").Should().HaveCount(2);
        fixture.Manager.Query("absent").Should().BeEmpty();
        await Task.Yield();

        persistence.LoadCount.Should().Be(0);
        persistence.SaveCount.Should().Be(0);
    }

    [Xunit.Fact]
    public async Task ZeroRetentionDoesNotKeepUnpinnedCopies()
    {
        var fixture = await Fixture.CreateAsync(new AuraSettings(RetentionDays: 0));
        await fixture.PublishAsync("not retained", 30);
        fixture.Manager.Query().Should().BeEmpty();
    }

    [Xunit.Fact]
    public async Task SilentCaptureUsesRealDispatcherWithoutRuntimeStateOrNotificationProjection()
    {
        var events = new EventDispatcher();
        var stateMachine = new StateMachine();
        var projection = new CountingNotificationSink();
        var pipeline = new RuntimeDecisionPipeline(new ContextEngine(), new PriorityEngine(), new PolicyEvaluator(),
            new FixedSettingsStore(new AuraSettings()), stateMachine);
        var runtime = new RuntimeCore(events, NullLogger<RuntimeCore>.Instance, pipeline, projection);
        var persistence = new MemoryPersistence();
        var manager = new ClipboardArtifactManager(persistence, new FakeClipboardWriter(), new FixedSettingsStore(new AuraSettings()),
            events, new ClipboardSensitivityClassifier(), TimeProvider.System);
        await manager.StartAsync();
        await runtime.StartAsync();
        var initialState = stateMachine.Current;
        for (var i = 0; i < 5; i++)
            await events.PublishAsync(new ClipboardCaptureEvent(DateTimeOffset.UtcNow, $"silent {i}", null, 200 + i));

        manager.Query().Should().HaveCount(5);
        stateMachine.Current.Should().Be(initialState);
        projection.Count.Should().Be(0);
        runtime.LastEvaluation.Should().BeNull();
        await runtime.StopAsync();
        await manager.StopAsync();
    }

    [Xunit.Fact]
    public void SensitivityClassifierIsConservativeAndHasPositiveNegativeCases()
    {
        var classifier = new ClipboardSensitivityClassifier();
        classifier.IsSensitive("password = hunter2").Should().BeTrue();
        classifier.IsSensitive("-----BEGIN PRIVATE KEY-----").Should().BeTrue();
        classifier.IsSensitive("Authorization: Bearer abcdef0123456789").Should().BeTrue();
        classifier.IsSensitive("This prose discusses a password manager but contains no credential.").Should().BeFalse();
        classifier.IsSensitive("Ordinary copied text").Should().BeFalse();
    }

    [Xunit.Fact]
    public async Task PersistenceRoundTripsPinnedItemsAndRecoversFromMissingOrCorruptFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "clipboard.json");
        try
        {
            var store = new JsonClipboardArtifactPersistence(path);
            (await store.LoadAsync()).Should().BeEmpty();
            var items = new[] { new ClipboardArtifact("id", "pin me", DateTimeOffset.UtcNow, true, false, null) };
            await store.SaveAsync(items);
            (await new JsonClipboardArtifactPersistence(path).LoadAsync()).Should().BeEquivalentTo(items);
            await File.WriteAllTextAsync(path, "{ truncated");
            (await store.LoadAsync()).Should().BeEmpty();
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    [Xunit.Fact]
    public async Task FailedAtomicWritePreservesPreviouslyCommittedFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "history.json");
        try
        {
            var store = new JsonClipboardArtifactPersistence(path);
            var existing = new[] { new ClipboardArtifact("existing", "valid", DateTimeOffset.UtcNow, false, false, null) };
            await store.SaveAsync(existing);
            var originalBytes = await File.ReadAllBytesAsync(path);
            var failingPersistence = new JsonClipboardArtifactPersistence(directory);
            Func<Task> write = async () => await failingPersistence.SaveAsync([new ClipboardArtifact("new", "replacement", DateTimeOffset.UtcNow, false, false, null)]);
            var exception = await write.Should().ThrowAsync<Exception>();
            (exception.Which is IOException || exception.Which is UnauthorizedAccessException).Should().BeTrue();
            (await File.ReadAllBytesAsync(path)).Should().Equal(originalBytes);
            (await store.LoadAsync()).Should().BeEquivalentTo(existing);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    [Xunit.Fact]
    public async Task PinAndDeleteRollbackWhenPersistenceFails()
    {
        var persistence = new ToggleFailurePersistence();
        var fixture = await Fixture.CreateAsync(new AuraSettings(), persistence);
        await fixture.PublishAsync("persisted item", 90);
        var item = fixture.Manager.Query().Single();
        persistence.FailWrites = true;

        Func<Task> pin = async () => await fixture.Manager.SetPinnedAsync(item.Id, true);
        await pin.Should().ThrowAsync<IOException>();
        fixture.Manager.Query().Single().IsPinned.Should().BeFalse();

        Func<Task> delete = async () => await fixture.Manager.DeleteAsync(item.Id);
        await delete.Should().ThrowAsync<IOException>();
        fixture.Manager.Query().Should().ContainSingle().Which.Id.Should().Be(item.Id);
    }

    [Xunit.Fact]
    public async Task ClipboardContentNeverAppearsInDiagnosticLogging()
    {
        const string canary = "DIAGNOSTIC-CANARY-30f9 Password=hidden";
        var fixture = await Fixture.CreateAsync(new AuraSettings(AllowSensitiveData: true));
        await fixture.PublishAsync(canary, 600);
        var item = fixture.Manager.Query().Single();
        await fixture.Manager.RestoreAsync(item.Id);
        await fixture.Manager.DeleteAsync(item.Id);
        string.Join("\n", fixture.Logs).Should().NotContain("DIAGNOSTIC-CANARY-30f9");
    }

    [Xunit.Fact]
    public async Task PinnedEntriesRespectMaximumHistoryCapacity()
    {
        var persistence = new MemoryPersistence();
        var fixture = await Fixture.CreateAsync(new AuraSettings(), persistence);
        for (var i = 0; i < ClipboardArtifactManager.MaximumHistoryItems; i++)
            await fixture.PublishAsync($"item {i}", i + 1);
        foreach (var item in fixture.Manager.Query())
            (await fixture.Manager.SetPinnedAsync(item.Id, true)).Should().BeTrue();

        await fixture.PublishAsync("over capacity", 1000);
        fixture.Manager.Query().Should().HaveCount(ClipboardArtifactManager.MaximumHistoryItems);
        fixture.Manager.Query().Should().OnlyContain(item => item.IsPinned);
    }

    [Xunit.Fact]
    public async Task PinUnpinAndDeleteUpdateOwnedHistory()
    {
        var fixture = await Fixture.CreateAsync(new AuraSettings());
        await fixture.PublishAsync("lifecycle", 40);
        var artifact = fixture.Manager.Query().Single();
        (await fixture.Manager.SetPinnedAsync(artifact.Id, true)).Should().BeTrue();
        fixture.Manager.Query().Single().IsPinned.Should().BeTrue();
        (await fixture.Manager.SetPinnedAsync(artifact.Id, false)).Should().BeTrue();
        (await fixture.Manager.DeleteAsync(artifact.Id)).Should().BeTrue();
        fixture.Manager.Query().Should().BeEmpty();
    }

    private sealed class Fixture
    {
        private Fixture(ClipboardArtifactManager manager, EventDispatcher events, List<string> logs, IClipboardArtifactPersistence persistence, FakeClipboardWriter writer, FakeClock clock)
            => (Manager, Events, Logs, Persistence, Writer, Clock) = (manager, events, logs, persistence, writer, clock);
        public ClipboardArtifactManager Manager { get; }
        public EventDispatcher Events { get; }
        public List<string> Logs { get; }
        public IClipboardArtifactPersistence Persistence { get; }
        public FakeClipboardWriter Writer { get; }
        public FakeClock Clock { get; }
        public static async Task<Fixture> CreateAsync(AuraSettings settings, IClipboardArtifactPersistence? persistence = null, FakeClipboardWriter? writer = null)
        {
            var events = new EventDispatcher();
            var store = new FixedSettingsStore(settings);
            var logs = new List<string>();
            writer ??= new FakeClipboardWriter();
            persistence ??= new MemoryPersistence();
            var clock = new FakeClock();
            var manager = new ClipboardArtifactManager(persistence, writer, store, events, new ClipboardSensitivityClassifier(), clock);
            await manager.StartAsync();
            return new Fixture(manager, events, logs, persistence, writer, clock);
        }
        public ValueTask PublishAsync(string text, long sequence)
        {
            var timestamp = DateTimeOffset.UnixEpoch.AddTicks(sequence);
            Clock.Set(timestamp);
            return Events.PublishAsync(new ClipboardCaptureEvent(timestamp, text, null, sequence));
        }
        public ValueTask PublishAsync(string text, DateTimeOffset timestamp)
        {
            Clock.Set(timestamp);
            return Events.PublishAsync(new ClipboardCaptureEvent(timestamp, text, null, timestamp.Ticks));
        }
    }

    private sealed class FakeClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UnixEpoch;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Set(DateTimeOffset now) => _now = now;
    }

    private sealed class ToggleFailurePersistence : IClipboardArtifactPersistence
    {
        private readonly MemoryPersistence _inner = new();
        public bool FailWrites { get; set; }
        public ValueTask<IReadOnlyList<ClipboardArtifact>> LoadAsync(CancellationToken cancellationToken = default) => _inner.LoadAsync(cancellationToken);
        public ValueTask SaveAsync(IReadOnlyList<ClipboardArtifact> items, CancellationToken cancellationToken = default)
        {
            if (FailWrites) throw new IOException("Injected storage failure.");
            return _inner.SaveAsync(items, cancellationToken);
        }
    }


    private sealed class MemoryPersistence : IClipboardArtifactPersistence
    {
        private IReadOnlyList<ClipboardArtifact> _items = Array.Empty<ClipboardArtifact>();
        public string Serialized { get; private set; } = string.Empty;
        public ValueTask<IReadOnlyList<ClipboardArtifact>> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(_items);
        public ValueTask SaveAsync(IReadOnlyList<ClipboardArtifact> items, CancellationToken cancellationToken = default)
        {
            _items = items.ToArray();
            Serialized = System.Text.Json.JsonSerializer.Serialize(items);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class CountingPersistence : IClipboardArtifactPersistence
    {
        private readonly MemoryPersistence _inner = new();
        private int _loadCount;
        private int _saveCount;
        public int LoadCount => Volatile.Read(ref _loadCount);
        public int SaveCount => Volatile.Read(ref _saveCount);
        public void ResetCounts()
        {
            Interlocked.Exchange(ref _loadCount, 0);
            Interlocked.Exchange(ref _saveCount, 0);
        }
        public async ValueTask<IReadOnlyList<ClipboardArtifact>> LoadAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _loadCount);
            return await _inner.LoadAsync(cancellationToken);
        }
        public async ValueTask SaveAsync(IReadOnlyList<ClipboardArtifact> items, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _saveCount);
            await _inner.SaveAsync(items, cancellationToken);
        }
    }

    private sealed class FakeClipboardWriter : IClipboardTextWriter
    {
        public long NextSequence { get; set; } = 100;
        public Action<string, long>? OnSetText { get; set; }
        public Func<Task>? BeforeSetText { get; set; }
        public int LastWriterThreadId { get; private set; }
        public bool TrySetText(string text, out long sequenceNumber)
        {
            LastWriterThreadId = Environment.CurrentManagedThreadId;
            BeforeSetText?.Invoke().GetAwaiter().GetResult();
            sequenceNumber = NextSequence;
            OnSetText?.Invoke(text, sequenceNumber);
            return true;
        }
    }

    private sealed class CountingNotificationSink : INotificationProjectionSink
    {
        public int Count { get; private set; }
        public void Project(AuraNotification notification, RuntimeDecision decision) => Count++;
    }

    private sealed class FixedSettingsStore(AuraSettings current) : ISettingsStore
    {
        public AuraSettings Current { get; } = current;
        public event EventHandler<AuraSettings>? Changed { add { } remove { } }
        public ValueTask<AuraSettings> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(Current);
        public ValueTask SaveAsync(AuraSettings settings, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
