using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aura.Runtime.Media;

public sealed class MediaControlService : IMediaControlService
{
    private readonly IMediaPlatformAdapter _media;
    private readonly ISystemVolumeAdapter _volume;
    private readonly ILogger<MediaControlService> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly object _lifecycleLock = new();
    private readonly object _projectionLock = new();

    private MediaProjection _current = MediaProjection.Unavailable;
    private CancellationTokenSource? _lifetime;
    private Task? _refreshTask;
    private bool _refreshPending;
    private long _refreshGeneration;
    private bool _started;
    private bool _disposed;

    public MediaControlService(IMediaPlatformAdapter media, ISystemVolumeAdapter volume, ILogger<MediaControlService> logger)
    {
        _media = media;
        _volume = volume;
        _logger = logger;
    }

    public MediaProjection Current => Volatile.Read(ref _current);
    public event EventHandler<MediaProjection>? Changed;

    public async Task WaitForProjectionAsync(Func<MediaProjection, bool> predicate, CancellationToken cancellationToken = default)
    {
        if (predicate(Current)) return;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<MediaProjection>? handler = null;
        handler = (_, projection) =>
        {
            if (predicate(projection)) completion.TrySetResult();
        };
        Changed += handler;
        try
        {
            if (predicate(Current)) return;
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
        }
        finally { Changed -= handler; }
    }

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_started) return;
            _started = true;
            _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _media.Changed += OnPlatformChanged;
            _volume.VolumeChanged += OnVolumeChanged;
        }

        try
        {
            await RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Media session initialization failed; media controls are unavailable.");
            Publish(MediaProjection.Unavailable);
        }
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        CancellationTokenSource? lifetime;
        Task? refresh;
        lock (_lifecycleLock)
        {
            if (!_started) return;
            _started = false;
            _disposed = true;
            _media.Changed -= OnPlatformChanged;
            _volume.VolumeChanged -= OnVolumeChanged;
            lifetime = _lifetime;
            _lifetime = null;
            refresh = _refreshTask;
        }

        lifetime?.Cancel();
        if (refresh is not null)
        {
            try { await refresh.WaitAsync(cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (lifetime?.IsCancellationRequested == true) { }
        }
        lifetime?.Dispose();
        await _media.DisposeAsync().ConfigureAwait(false);
        await _volume.DisposeAsync().ConfigureAwait(false);
        Publish(MediaProjection.Unavailable);
    }

    public async ValueTask<MediaCommandResult> ExecuteAsync(MediaCommand command, CancellationToken cancellationToken = default)
    {
        var session = Current.Session;
        if (string.IsNullOrEmpty(session.SessionId)) return MediaCommandResult.NoSession;
        var supported = command switch
        {
            MediaCommand.PlayPause => session.CanPlayPause,
            MediaCommand.Previous => session.CanGoPrevious,
            MediaCommand.Next => session.CanGoNext,
            _ => false
        };
        if (!supported) return MediaCommandResult.Unsupported;

        try
        {
            return await _media.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Media command {Command} failed.", command);
            return MediaCommandResult.Failed;
        }
    }

    public async ValueTask<bool> SetVolumeAsync(double level, CancellationToken cancellationToken = default)
    {
        if (!double.IsFinite(level)) return false;
        try
        {
            return await _volume.SetVolumeAsync(Math.Clamp(level, 0d, 1d), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "System volume change failed.");
            return false;
        }
    }

    private void OnPlatformChanged(object? sender, EventArgs e) => QueueRefresh();

    private void OnVolumeChanged(object? sender, double level)
    {
        _ = RefreshVolumeFromEndpointAsync();
    }

    private async Task RefreshVolumeFromEndpointAsync()
    {
        double? level;
        try { level = await _volume.GetVolumeAsync().ConfigureAwait(false); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "System volume refresh failed after an endpoint notification.");
            level = null;
        }

        var bounded = level is { } value && double.IsFinite(value) ? Math.Clamp(value, 0d, 1d) : (double?)null;

        MediaProjection? updated = null;
        lock (_projectionLock)
        {
            var current = Current;
            var projection = current with { Volume = bounded is { } currentLevel ? new MediaVolumeSnapshot(true, currentLevel) : MediaVolumeSnapshot.Unavailable };
            if (!Equals(current, projection))
            {
                Volatile.Write(ref _current, projection);
                updated = projection;
            }
        }
        if (updated is not null)
        {
            Changed?.Invoke(this, updated);
        }
    }


    private void QueueRefresh()
    {
        CancellationToken token;
        lock (_lifecycleLock)
        {
            if (!_started || _lifetime is null) return;
            token = _lifetime.Token;
            _refreshGeneration++;
            if (_refreshTask is { IsCompleted: false })
            {
                _refreshPending = true;
                return;
            }
            _refreshTask = RefreshFromEventAsync(token);
        }
    }

    private async Task RefreshFromEventAsync(CancellationToken cancellationToken)
    {
        do
        {
            lock (_lifecycleLock) _refreshPending = false;
            try { await RefreshAsync(cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Media state refresh failed; publishing an unavailable projection.");
                Publish(MediaProjection.Unavailable);
            }

            lock (_lifecycleLock)
            {
                if (!_started || cancellationToken.IsCancellationRequested || !_refreshPending) return;
            }
        } while (true);
    }

    private async ValueTask RefreshAsync(CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            long generation;
            lock (_lifecycleLock) generation = _refreshGeneration;
            var session = await _media.GetCurrentSessionAsync(cancellationToken).ConfigureAwait(false);
            var volume = await _volume.GetVolumeAsync(cancellationToken).ConfigureAwait(false);
            var volumeSnapshot = volume is { } level && double.IsFinite(level)
                ? new MediaVolumeSnapshot(true, Math.Clamp(level, 0d, 1d))
                : MediaVolumeSnapshot.Unavailable;
            lock (_lifecycleLock)
            {
                if (!_started || cancellationToken.IsCancellationRequested || generation != _refreshGeneration) return;
                Publish(new MediaProjection(session ?? MediaSessionSnapshot.NoSession, volumeSnapshot));
            }
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private void Publish(MediaProjection projection)
    {
        lock (_projectionLock)
        {
            if (Equals(Current, projection)) return;
            Volatile.Write(ref _current, projection);
        }
        Changed?.Invoke(this, projection);
    }
}
