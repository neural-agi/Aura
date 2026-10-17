using Aura.Runtime.Context;
using Aura.Runtime.Decision;
using Aura.Runtime.Policy;
using Aura.Runtime.Priority;

namespace Aura.Runtime.Notifications;

public sealed class NotificationIngestionService(
    INotificationProvider provider,
    IEventDispatcher eventDispatcher,
    NotificationIdentitySet identities)
{
    public async ValueTask<NotificationProviderStatus> PollOnceAsync(
        ContextInput context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var result = await provider.GetNotificationsAsync(cancellationToken).ConfigureAwait(false);
        if (result.Status != NotificationProviderStatus.Available)
        {
            return result.Status;
        }

        foreach (var source in result.Notifications)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var notification = NotificationNormalizer.Normalize(source);
            if (!identities.TryAccept(notification))
            {
                continue;
            }

            var input = new RuntimeEvaluationInput(
                context,
                new PriorityInput(notification.Id, IsTimeSensitive: true,
                    IsNovel: true, IsContextRelevant: false, OccurredAtUtc: notification.Timestamp),
                new PolicyRequest(PolicyResource.SensitiveData, "surface-notification", IsSensitive: true,
                    AuthorizationKnown: false, IsAuthorized: false),
                notification);
            await eventDispatcher.PublishAsync(
                new RuntimeEvaluationRequestedEvent(notification.Timestamp, input), cancellationToken).ConfigureAwait(false);
        }

        return NotificationProviderStatus.Available;
    }
}
