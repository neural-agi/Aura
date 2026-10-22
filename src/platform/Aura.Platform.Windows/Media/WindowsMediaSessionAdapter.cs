
using Aura.Runtime.Media;
using Windows.Media.Control;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace Aura.Platform.Windows.Media;

public sealed class WindowsMediaSessionAdapter : IMediaPlatformAdapter
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _currentSession;
    private TypedEventHandler<GlobalSystemMediaTransportControlsSessionManager, CurrentSessionChangedEventArgs>? _currentSessionChangedHandler;
    private TypedEventHandler<GlobalSystemMediaTransportControlsSessionManager, SessionsChangedEventArgs>? _sessionsChangedHandler;
    private TypedEventHandler<GlobalSystemMediaTransportControlsSession, MediaPropertiesChangedEventArgs>? _propertiesChangedHandler;
    private TypedEventHandler<GlobalSystemMediaTransportControlsSession, PlaybackInfoChangedEventArgs>? _playbackChangedHandler;
    private bool _disposed;

    public event EventHandler? Changed;

    public async ValueTask<MediaSessionSnapshot> GetCurrentSessionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (_currentSessionChangedHandler is null)
            {
                _currentSessionChangedHandler = (_, _) => RaiseChanged();
                _sessionsChangedHandler = (_, _) => RaiseChanged();
                _manager.CurrentSessionChanged += _currentSessionChangedHandler;
                _manager.SessionsChanged += _sessionsChangedHandler;
            }

            var next = _manager.GetCurrentSession();
            if (!ReferenceEquals(next, _currentSession))
            {
                UnsubscribeSession();
                _currentSession = next;
                SubscribeSession(next);
            }

            if (next is null) return MediaSessionSnapshot.NoSession;
            try { return await SnapshotSessionAsync(next, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                return MediaSessionSnapshot.NoSession;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<MediaCommandResult> ExecuteAsync(MediaCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var session = _manager?.GetCurrentSession();
            if (session is null) return MediaCommandResult.NoSession;
            var playback = session.GetPlaybackInfo();
            var supported = command switch
            {
                MediaCommand.PlayPause => playback.Controls.IsPlayPauseToggleEnabled,
                MediaCommand.Previous => playback.Controls.IsPreviousEnabled,
                MediaCommand.Next => playback.Controls.IsNextEnabled,
                _ => false
            };
            if (!supported) return MediaCommandResult.Unsupported;
            var result = command switch
            {
                MediaCommand.PlayPause => await session.TryTogglePlayPauseAsync(),
                MediaCommand.Previous => await session.TrySkipPreviousAsync(),
                MediaCommand.Next => await session.TrySkipNextAsync(),
                _ => false
            };
            return result ? MediaCommandResult.Succeeded : MediaCommandResult.Failed;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async ValueTask<MediaSessionSnapshot> SnapshotSessionAsync(GlobalSystemMediaTransportControlsSession session, CancellationToken cancellationToken)
    {
        var playbackInfo = session.GetPlaybackInfo();
        var properties = await session.TryGetMediaPropertiesAsync();
        cancellationToken.ThrowIfCancellationRequested();
        byte[]? artwork = null;
        string? artworkType = null;
        if (properties.Thumbnail is { } thumbnail)
        {
            try
            {
                using var stream = await thumbnail.OpenReadAsync();
                using var reader = new DataReader(stream.GetInputStreamAt(0));
                var size = checked((int)stream.Size);
                if (size == 0 || size > 5 * 1024 * 1024) throw new InvalidDataException("Media artwork size is outside the supported range.");
                var bytes = new byte[size];
                await reader.LoadAsync((uint)bytes.Length);
                reader.ReadBytes(bytes);
                artwork = bytes;
                artworkType = GuessImageContentType(bytes);
            }
            catch (Exception)
            {
                artwork = null;
                artworkType = null;
            }
        }

        var source = EmptyToNull(session.SourceAppUserModelId);
        return new MediaSessionSnapshot(
            source ?? "Current system session",
            source,
            EmptyToNull(properties.Title),
            EmptyToNull(properties.Artist),
            EmptyToNull(properties.AlbumTitle),
            artwork,
            artworkType,
            playbackInfo.PlaybackStatus switch
            {
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => MediaPlaybackState.Playing,
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => MediaPlaybackState.Paused,
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped => MediaPlaybackState.Stopped,
                _ => MediaPlaybackState.Unknown
            },
            playbackInfo.Controls.IsPlayPauseToggleEnabled,
            playbackInfo.Controls.IsPreviousEnabled,
            playbackInfo.Controls.IsNextEnabled);
    }

    private void SubscribeSession(GlobalSystemMediaTransportControlsSession? session)
    {
        if (session is null) return;
        _propertiesChangedHandler = (_, _) => RaiseChanged();
        _playbackChangedHandler = (_, _) => RaiseChanged();
        session.MediaPropertiesChanged += _propertiesChangedHandler;
        session.PlaybackInfoChanged += _playbackChangedHandler;
    }

    private void UnsubscribeSession()
    {
        if (_currentSession is not null)
        {
            if (_propertiesChangedHandler is not null) _currentSession.MediaPropertiesChanged -= _propertiesChangedHandler;
            if (_playbackChangedHandler is not null) _currentSession.PlaybackInfoChanged -= _playbackChangedHandler;
        }
        _propertiesChangedHandler = null;
        _playbackChangedHandler = null;
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private static string? GuessImageContentType(byte[] bytes)
    {
        if (bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "image/png";
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return "image/jpeg";
        if (bytes.Length >= 6 && (bytes.AsSpan(0, 6).SequenceEqual("GIF87a"u8) || bytes.AsSpan(0, 6).SequenceEqual("GIF89a"u8))) return "image/gif";
        return null;
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;
            _disposed = true;
            UnsubscribeSession();
            if (_manager is not null)
            {
                if (_currentSessionChangedHandler is not null) _manager.CurrentSessionChanged -= _currentSessionChangedHandler;
                if (_sessionsChangedHandler is not null) _manager.SessionsChanged -= _sessionsChangedHandler;
            }
            _currentSession = null;
            _manager = null;
        }
        finally
        {
            _gate.Release();
        }
    }
}
