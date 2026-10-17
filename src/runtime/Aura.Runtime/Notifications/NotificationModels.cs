using Aura.Runtime.Decision;

namespace Aura.Runtime.Notifications;

public sealed record NotificationAction(string Id, string Label);

public sealed record AuraNotification(
    string Id,
    string SourceApp,
    string Title,
    string Body,
    DateTimeOffset Timestamp,
    IReadOnlyList<NotificationAction> Actions);

public sealed record NotificationSourcePayload(
    string? SourceId,
    string SourceApp,
    string? Title,
    string? Body,
    DateTimeOffset Timestamp,
    IReadOnlyList<NotificationAction>? Actions = null);

public static class NotificationNormalizer
{
    public static AuraNotification Normalize(NotificationSourcePayload source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(source.SourceApp);

        var sourceApp = source.SourceApp.Trim();
        var title = source.Title?.Trim() ?? string.Empty;
        var body = source.Body?.Trim() ?? string.Empty;
        var id = string.IsNullOrWhiteSpace(source.SourceId)
            ? CreateFallbackId(sourceApp, source.Timestamp, title)
            : source.SourceId.Trim();

        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("A notification identity could not be determined.", nameof(source));
        }

        return new AuraNotification(id, sourceApp, title, body, source.Timestamp,
            Array.AsReadOnly((source.Actions ?? Array.Empty<NotificationAction>()).ToArray()));
    }

    private static string CreateFallbackId(string sourceApp, DateTimeOffset timestamp, string title)
    {
        var material = $"{sourceApp}\n{timestamp.ToUniversalTime():O}\n{title}";
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(material)));
    }
}

public sealed class NotificationIdentitySet
{
    private readonly HashSet<string> _identities = new(StringComparer.Ordinal);

    public bool TryAccept(AuraNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        return _identities.Add(notification.Id);
    }
}

public enum NotificationProviderStatus
{
    Available,
    Unavailable,
    AccessDenied
}

public sealed record NotificationProviderResult(
    NotificationProviderStatus Status,
    IReadOnlyList<NotificationSourcePayload> Notifications)
{
    public static NotificationProviderResult Unavailable { get; } = new(NotificationProviderStatus.Unavailable, Array.Empty<NotificationSourcePayload>());
    public static NotificationProviderResult AccessDenied { get; } = new(NotificationProviderStatus.AccessDenied, Array.Empty<NotificationSourcePayload>());
}

public interface INotificationProvider
{
    ValueTask<NotificationProviderResult> GetNotificationsAsync(CancellationToken cancellationToken = default);
}

public sealed class UnavailableNotificationProvider : INotificationProvider
{
    public ValueTask<NotificationProviderResult> GetNotificationsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(NotificationProviderResult.Unavailable);
    }
}

public interface INotificationProjectionSink
{
    void Project(AuraNotification notification, RuntimeDecision decision);
}


public sealed class NullNotificationProjectionSink : INotificationProjectionSink
{
    public void Project(AuraNotification notification, RuntimeDecision decision) { }
}
