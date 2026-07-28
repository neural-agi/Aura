namespace Aura.Runtime;

public sealed class RuntimeScheduler : IRuntimeScheduler, IDisposable
{
    private readonly object _gate = new();
    private readonly Queue<ScheduledWorkItem> _queue = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _runner;
    private bool _disposed;

    public RuntimeScheduler()
    {
        _runner = Task.Run(ProcessQueueAsync);
    }

    public ValueTask ScheduleAsync(Func<CancellationToken, ValueTask> work, TimeSpan delay, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        ThrowIfDisposed();

        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled(cancellationToken);
        }

        var item = new ScheduledWorkItem(work, delay, cancellationToken, _shutdown.Token);
        lock (_gate)
        {
            ThrowIfDisposed();
            _queue.Enqueue(item);
            Monitor.Pulse(_gate);
        }

        return new ValueTask(item.Completion.Task);
    }

    public ValueTask ScheduleAsync(Func<CancellationToken, ValueTask> work, CancellationToken cancellationToken = default)
        => ScheduleAsync(work, TimeSpan.Zero, cancellationToken);

    private async Task ProcessQueueAsync()
    {
        try
        {
            while (true)
            {
                ScheduledWorkItem item;

                lock (_gate)
                {
                    while (_queue.Count == 0 && !_shutdown.IsCancellationRequested)
                    {
                        Monitor.Wait(_gate);
                    }

                    if (_shutdown.IsCancellationRequested)
                    {
                        break;
                    }

                    item = _queue.Dequeue();
                }

                await ExecuteAsync(item).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }

        DrainPendingAsCanceled();
    }

    private async ValueTask ExecuteAsync(ScheduledWorkItem item)
    {
        try
        {
            item.CancellationToken.ThrowIfCancellationRequested();

            if (item.Delay > TimeSpan.Zero)
            {
                await Task.Delay(item.Delay, item.CancellationToken).ConfigureAwait(false);
            }

            item.CancellationToken.ThrowIfCancellationRequested();
            await item.Work(item.CancellationToken).ConfigureAwait(false);
            item.Completion.TrySetResult();
        }
        catch (OperationCanceledException)
        {
            item.Completion.TrySetCanceled(item.CancellationToken);
            throw;
        }
        catch (Exception ex)
        {
            item.Completion.TrySetException(ex);
        }
        finally
        {
            item.Dispose();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        _shutdown.Cancel();
        lock (_gate)
        {
            Monitor.PulseAll(_gate);
        }

        try
        {
            _runner.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }

        _shutdown.Dispose();
    }

    private void DrainPendingAsCanceled()
    {
        lock (_gate)
        {
            while (_queue.Count > 0)
            {
                var pending = _queue.Dequeue();
                pending.Completion.TrySetCanceled(pending.CancellationToken);
                pending.Dispose();
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(RuntimeScheduler));
        }
    }

    private sealed class ScheduledWorkItem : IDisposable
    {
        public ScheduledWorkItem(
            Func<CancellationToken, ValueTask> work,
            TimeSpan delay,
            CancellationToken cancellationToken,
            CancellationToken shutdownToken)
        {
            Work = work;
            Delay = delay;
            _linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, shutdownToken);
            CancellationToken = _linkedCancellation.Token;
            Completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public Func<CancellationToken, ValueTask> Work { get; }

        public TimeSpan Delay { get; }

        public CancellationToken CancellationToken { get; }

        public TaskCompletionSource Completion { get; }

        private readonly CancellationTokenSource _linkedCancellation;

        public void Dispose()
        {
            _linkedCancellation.Dispose();
        }
    }
}
