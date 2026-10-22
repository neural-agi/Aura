namespace Aura.Runtime.Media;

public enum MediaPlaybackState
{
    Unknown,
    Playing,
    Paused,
    Stopped
}

public sealed record MediaSessionSnapshot(
    string SessionId,
    string? SourceApplication,
    string? Title,
    string? Artist,
    string? Album,
    byte[]? Artwork,
    string? ArtworkContentType,
    MediaPlaybackState PlaybackState,
    bool CanPlayPause,
    bool CanGoPrevious,
    bool CanGoNext)
{
    public static MediaSessionSnapshot NoSession { get; } = new(
        string.Empty, null, null, null, null, null, null, MediaPlaybackState.Unknown, false, false, false);
}

public sealed record MediaVolumeSnapshot(bool IsAvailable, double? Level)
{
    public static MediaVolumeSnapshot Unavailable { get; } = new(false, null);
}

public sealed record MediaProjection(MediaSessionSnapshot Session, MediaVolumeSnapshot Volume)
{
    public static MediaProjection Unavailable { get; } = new(MediaSessionSnapshot.NoSession, MediaVolumeSnapshot.Unavailable);
}

public enum MediaCommand
{
    PlayPause,
    Previous,
    Next
}

public enum MediaCommandResult
{
    Succeeded,
    Unsupported,
    Failed,
    NoSession
}

public interface IMediaControlService
{
    MediaProjection Current { get; }
    event EventHandler<MediaProjection>? Changed;
    ValueTask StartAsync(CancellationToken cancellationToken = default);
    ValueTask StopAsync(CancellationToken cancellationToken = default);
    ValueTask<MediaCommandResult> ExecuteAsync(MediaCommand command, CancellationToken cancellationToken = default);
    ValueTask<bool> SetVolumeAsync(double level, CancellationToken cancellationToken = default);
}

public interface IMediaPlatformAdapter : IAsyncDisposable
{
    event EventHandler? Changed;
    ValueTask<MediaSessionSnapshot> GetCurrentSessionAsync(CancellationToken cancellationToken = default);
    ValueTask<MediaCommandResult> ExecuteAsync(MediaCommand command, CancellationToken cancellationToken = default);
}

public interface ISystemVolumeAdapter : IAsyncDisposable
{
    event EventHandler<double>? VolumeChanged;
    ValueTask<double?> GetVolumeAsync(CancellationToken cancellationToken = default);
    ValueTask<bool> SetVolumeAsync(double level, CancellationToken cancellationToken = default);
}
