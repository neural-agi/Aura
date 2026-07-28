namespace Aura.Runtime;

public interface IRuntimeCore
{
    RuntimeState State { get; }

    RuntimeHealth Health { get; }

    ValueTask StartAsync(CancellationToken cancellationToken = default);

    ValueTask StopAsync(CancellationToken cancellationToken = default);
}

