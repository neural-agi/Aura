namespace Aura.Runtime;

public interface IRuntimeScheduler
{
    ValueTask ScheduleAsync(Func<CancellationToken, ValueTask> work, TimeSpan delay, CancellationToken cancellationToken = default);

    ValueTask ScheduleAsync(Func<CancellationToken, ValueTask> work, CancellationToken cancellationToken = default);
}

