using Aura.Runtime.Media;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aura.Tests.Unit;

public sealed class MediaControlServiceTests
{
    [Xunit.Fact]
    public async Task StartsWithNoSessionAndDoesNotTransitionAuraState()
    {
        var adapter = new FakeMediaAdapter();
        await using var volume = new FakeVolumeAdapter();
        var service = new MediaControlService(adapter, volume, NullLogger<MediaControlService>.Instance);
        var machine = new Aura.Runtime.StateMachine();
        var initialState = machine.Current;
        var observed = new List<MediaProjection>();
        service.Changed += (_, value) => observed.Add(value);

        await service.StartAsync();

        service.Current.Session.SessionId.Should().BeEmpty();
        observed.Should().BeEmpty();
        machine.Current.Should().Be(initialState);
        await service.StopAsync();
    }

    [Xunit.Fact]
    public async Task ProjectsSessionChangesAndMetadataWithoutPersistingIt()
    {
        var adapter = new FakeMediaAdapter
        {
            Snapshot = new MediaSessionSnapshot("session-a", "player.app", "A title", "An artist", "An album", [1, 2], "image/png", MediaPlaybackState.Playing, true, false, true)
        };
        await using var volume = new FakeVolumeAdapter { Level = 0.4 };
        var service = new MediaControlService(adapter, volume, NullLogger<MediaControlService>.Instance);
        var observed = new List<MediaProjection>();
        service.Changed += (_, value) => observed.Add(value);

        await service.StartAsync();
        service.Current.Session.Should().Be(adapter.Snapshot);
        service.Current.Volume.Level.Should().BeApproximately(0.4, 0.001);
        service.Current.Session.Artwork.Should().Equal(1, 2);

        adapter.Snapshot = MediaSessionSnapshot.NoSession;
        adapter.RaiseChanged();
        await adapter.WaitForRefreshAsync();
        service.Current.Session.SessionId.Should().BeEmpty();

        adapter.Snapshot = new MediaSessionSnapshot("session-b", "other.app", "Updated", null, null, null, null, MediaPlaybackState.Paused, false, false, false);
        adapter.RaiseChanged();
        await adapter.WaitForRefreshAsync();
        service.Current.Session.Title.Should().Be("Updated");
        service.Current.Session.PlaybackState.Should().Be(MediaPlaybackState.Paused);
        service.Current.Session.CanPlayPause.Should().BeFalse();
        observed.Should().Contain(projection => projection.Session.Title == "A title");
        await service.StopAsync();
        adapter.EventSubscribers.Should().Be(0);
    }

    [Xunit.Fact]
    public async Task CommandsAreCapabilityAwareAndVolumeIsClamped()
    {
        var adapter = new FakeMediaAdapter
        {
            Snapshot = new MediaSessionSnapshot("session-a", "player", "Title", null, null, null, null, MediaPlaybackState.Paused, true, false, true)
        };
        await using var volume = new FakeVolumeAdapter { Level = 0.2 };
        var service = new MediaControlService(adapter, volume, NullLogger<MediaControlService>.Instance);
        await service.StartAsync();

        (await service.ExecuteAsync(MediaCommand.PlayPause)).Should().Be(MediaCommandResult.Succeeded);
        (await service.ExecuteAsync(MediaCommand.Previous)).Should().Be(MediaCommandResult.Unsupported);
        adapter.Result = MediaCommandResult.Failed;
        (await service.ExecuteAsync(MediaCommand.Next)).Should().Be(MediaCommandResult.Failed);
        (await service.SetVolumeAsync(2)).Should().BeTrue();
        volume.LastSet.Should().Be(1);
        volume.Level = 0.7;
        volume.RaiseVolumeChanged();
        service.Current.Volume.Level.Should().BeApproximately(0.7, 0.001);
        await service.StopAsync();
    }

    [Xunit.Fact]
    public async Task EventDuringRefreshCausesLatestSnapshotToBeProjected()
    {
        var adapter = new FakeMediaAdapter();
        await using var volume = new FakeVolumeAdapter();
        var service = new MediaControlService(adapter, volume, NullLogger<MediaControlService>.Instance);
        await service.StartAsync();

        adapter.BlockNextRead();
        adapter.RaiseChanged();
        await adapter.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        adapter.Snapshot = new MediaSessionSnapshot("session-latest", "player", "Latest", null, null, null, null, MediaPlaybackState.Playing, true, true, true);
        adapter.RaiseChanged();
        adapter.ReleaseRead();

        await service.WaitForProjectionAsync(projection => projection.Session.Title == "Latest");
        service.Current.Session.Title.Should().Be("Latest");
        await service.StopAsync();
    }

    [Xunit.Fact]
    public async Task EventDuringSnapshotReadDoesNotPublishStaleSessionAndFollowsUpWithNewest()
    {
        var stale = new MediaSessionSnapshot("old", "old source", "Stale", null, null, [1], "image/png", MediaPlaybackState.Playing, true, true, true);
        var newest = new MediaSessionSnapshot("new", "new source", "Newest", null, null, null, null, MediaPlaybackState.Paused, true, false, true);
        var adapter = new FakeMediaAdapter { Snapshot = stale };
        await using var volume = new FakeVolumeAdapter();
        var service = new MediaControlService(adapter, volume, NullLogger<MediaControlService>.Instance);
        await service.StartAsync();
        adapter.BlockNextRead();
        adapter.Snapshot = stale;
        adapter.RaiseChanged();
        await adapter.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        adapter.Snapshot = newest;
        adapter.RaiseChanged();
        adapter.ReleaseRead();

        await service.WaitForProjectionAsync(projection => projection.Session.SessionId == "new");

        service.Current.Session.Should().Be(newest);
        service.Current.Session.Artwork.Should().BeNull();
        adapter.RefreshCount.Should().Be(3);
        await service.StopAsync();
    }

    [Xunit.Fact]
    public async Task AdapterFailureDegradesToUnavailableAndShutdownUnsubscribes()
    {
        var adapter = new FakeMediaAdapter { ThrowOnRefresh = true };
        await using var volume = new FakeVolumeAdapter();
        var service = new MediaControlService(adapter, volume, NullLogger<MediaControlService>.Instance);

        await service.StartAsync();

        service.Current.Session.SessionId.Should().BeEmpty();
        await service.StopAsync();
        adapter.EventSubscribers.Should().Be(0);
        volume.EventSubscribers.Should().Be(0);
    }

    private sealed class FakeMediaAdapter : IMediaPlatformAdapter
    {
        private TaskCompletionSource _refresh = NewSignal();
        private TaskCompletionSource? _readStarted;
        private TaskCompletionSource? _releaseRead;
        private EventHandler? _changed;
        public MediaSessionSnapshot Snapshot { get; set; } = MediaSessionSnapshot.NoSession;
        public int RefreshCount { get; private set; }
        public MediaCommandResult Result { get; set; } = MediaCommandResult.Succeeded;
        public bool ThrowOnRefresh { get; set; }
        public int EventSubscribers => _changed?.GetInvocationList().Length ?? 0;
        public event EventHandler? Changed { add => _changed += value; remove => _changed -= value; }
        public async ValueTask<MediaSessionSnapshot> GetCurrentSessionAsync(CancellationToken cancellationToken = default)
        {
            RefreshCount++;
            if (ThrowOnRefresh) throw new InvalidOperationException("adapter unavailable");
            var release = _releaseRead;
            if (release is not null)
            {
                _readStarted!.TrySetResult();
                await release.Task.WaitAsync(cancellationToken);
                _releaseRead = null;
                _readStarted = null;
            }
            return Snapshot;
        }
        public ValueTask<MediaCommandResult> ExecuteAsync(MediaCommand command, CancellationToken cancellationToken = default) => ValueTask.FromResult(Result);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public void RaiseChanged()
        {
            _refresh = NewSignal();
            _changed?.Invoke(this, EventArgs.Empty);
            _ = CompleteRefreshAsync();
        }
        private async Task CompleteRefreshAsync()
        {
            await Task.Yield();
            _refresh.TrySetResult();
        }
        public Task WaitForRefreshAsync() => _refresh.Task.WaitAsync(TimeSpan.FromSeconds(2));

        public TaskCompletionSource ReadStarted => _readStarted ?? throw new InvalidOperationException("No blocked read was requested.");
        public void BlockNextRead()
        {
            _readStarted = NewSignal();
            _releaseRead = NewSignal();
        }
        public void ReleaseRead() => _releaseRead?.TrySetResult();

        private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class FakeVolumeAdapter : ISystemVolumeAdapter
    {
        private EventHandler<double>? _changed;
        public double? Level { get; set; }
        public double? LastSet { get; private set; }
        public int EventSubscribers => _changed?.GetInvocationList().Length ?? 0;
        public event EventHandler<double>? VolumeChanged { add => _changed += value; remove => _changed -= value; }
        public ValueTask<double?> GetVolumeAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(Level);
        public ValueTask<bool> SetVolumeAsync(double level, CancellationToken cancellationToken = default) { LastSet = level; return ValueTask.FromResult(true); }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public void RaiseVolumeChanged() => _changed?.Invoke(this, Level!.Value);
    }
}
