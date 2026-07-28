namespace Aura.Runtime;

public interface IEventDispatcher
{
    ValueTask PublishAsync(RuntimeEvent runtimeEvent, CancellationToken cancellationToken = default);

    IDisposable Subscribe(Func<RuntimeEvent, CancellationToken, ValueTask> handler);
}

