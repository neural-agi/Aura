
namespace Aura.UI.Surface;

internal sealed class LatestValueRenderQueue<T>(Action<T> render, Action<Action> enqueue, Action enqueueFailed)
    where T : class
{
    private readonly object _gate = new();
    private T? _pending;
    private bool _scheduled;



    public void Submit(T value)
    {
        var shouldEnqueue = false;

        lock (_gate)
        {
            _pending = value;
            if (!_scheduled)
            {
                _scheduled = true;
                shouldEnqueue = true;
            }
        }

        if (shouldEnqueue) EnqueueDrain();
    }

    private void EnqueueDrain()
    {
        try { enqueue(Drain); }
        catch
        {
            lock (_gate)
            {
                _scheduled = false;
                _pending = null;
            }
            enqueueFailed();
        }
    }

    private void Drain()
    {
        T? value;
        lock (_gate)
        {
            value = _pending;
            _pending = null;
        }

        if (value is not null) render(value);

        var followUp = false;
        lock (_gate)
        {
            if (_pending is null)
                _scheduled = false;
            else
                followUp = true;
        }

        if (followUp) EnqueueDrain();
    }
}
