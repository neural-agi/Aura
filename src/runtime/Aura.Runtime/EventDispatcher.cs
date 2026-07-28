namespace Aura.Runtime;

public sealed class EventDispatcher : IEventDispatcher
{
    private readonly object _gate = new();
    private readonly List<HandlerSubscription> _handlers = new();

    public ValueTask PublishAsync(RuntimeEvent runtimeEvent, CancellationToken cancellationToken = default)
    {
        HandlerSubscription[] snapshot;
        lock (_gate)
        {
            snapshot = _handlers.ToArray();
        }

        foreach (var handler in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var invocation = handler.Handler(runtimeEvent, cancellationToken);
            if (!invocation.IsCompletedSuccessfully)
            {
                return InvokeRemainingAsync(runtimeEvent, cancellationToken, invocation, snapshot, Array.IndexOf(snapshot, handler));
            }
        }

        return ValueTask.CompletedTask;
    }

    private async ValueTask InvokeRemainingAsync(
        RuntimeEvent runtimeEvent,
        CancellationToken cancellationToken,
        ValueTask firstInvocation,
        HandlerSubscription[] snapshot,
        int currentIndex)
    {
        await firstInvocation.ConfigureAwait(false);

        for (var i = currentIndex + 1; i < snapshot.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await snapshot[i].Handler(runtimeEvent, cancellationToken).ConfigureAwait(false);
        }
    }

    public IDisposable Subscribe(Func<RuntimeEvent, CancellationToken, ValueTask> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var subscription = new HandlerSubscription(Guid.NewGuid(), handler);
        lock (_gate)
        {
            _handlers.Add(subscription);
        }

        return new Subscription(this, subscription.Id);
    }

    private bool Remove(Guid id)
    {
        lock (_gate)
        {
            var index = _handlers.FindIndex(subscription => subscription.Id == id);
            if (index < 0)
            {
                return false;
            }

            _handlers.RemoveAt(index);
            return true;
        }
    }

    private readonly record struct HandlerSubscription(Guid Id, Func<RuntimeEvent, CancellationToken, ValueTask> Handler);

    private sealed class Subscription : IDisposable
    {
        private readonly EventDispatcher _dispatcher;
        private readonly Guid _id;
        private int _disposed;

        public Subscription(EventDispatcher dispatcher, Guid id)
        {
            _dispatcher = dispatcher;
            _id = id;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _dispatcher.Remove(_id);
            }
        }
    }
}
