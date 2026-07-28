using Microsoft.Extensions.Logging;

namespace Aura.Runtime;

public sealed class RuntimeCore : IRuntimeCore
{
    private readonly IEventDispatcher _eventDispatcher;
    private readonly ILogger<RuntimeCore> _logger;
    private readonly object _gate = new();

    private RuntimeState _state = RuntimeState.Created;
    private RuntimeHealth _health = RuntimeHealth.Unknown;
    private bool _started;

    public RuntimeCore(IEventDispatcher eventDispatcher, ILogger<RuntimeCore> logger)
    {
        _eventDispatcher = eventDispatcher;
        _logger = logger;
    }

    public RuntimeState State
    {
        get { lock (_gate) return _state; }
    }

    public RuntimeHealth Health
    {
        get { lock (_gate) return _health; }
    }

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        RuntimeState priorState;
        lock (_gate)
        {
            if (_started)
            {
                return;
            }

            _started = true;
            priorState = _state;
            _state = RuntimeState.Initializing;
            _health = RuntimeHealth.Unknown;
        }

        try
        {
            await _eventDispatcher.PublishAsync(new RuntimeStartedEvent(DateTimeOffset.UtcNow), cancellationToken).ConfigureAwait(false);

            lock (_gate)
            {
                _state = RuntimeState.Running;
                _health = RuntimeHealth.Healthy;
            }

            _logger.LogInformation("Aura runtime started.");
        }
        catch (OperationCanceledException)
        {
            lock (_gate)
            {
                _state = priorState;
                _health = RuntimeHealth.Degraded;
                _started = false;
            }

            _logger.LogWarning("Aura runtime startup was canceled.");
            throw;
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                _state = RuntimeState.Faulted;
                _health = RuntimeHealth.Faulted;
                _started = false;
            }

            _logger.LogCritical(ex, "Aura runtime failed to start.");
            throw;
        }
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        bool shouldStop;
        lock (_gate)
        {
            shouldStop = _started && _state is RuntimeState.Running or RuntimeState.Initializing;
            if (shouldStop)
            {
                _state = RuntimeState.Stopping;
                _health = RuntimeHealth.Degraded;
            }
        }

        if (!shouldStop)
        {
            return;
        }

        try
        {
            await _eventDispatcher.PublishAsync(new RuntimeStoppingEvent(DateTimeOffset.UtcNow), cancellationToken).ConfigureAwait(false);
            await _eventDispatcher.PublishAsync(new RuntimeStoppedEvent(DateTimeOffset.UtcNow), cancellationToken).ConfigureAwait(false);

            lock (_gate)
            {
                _state = RuntimeState.Stopped;
                _health = RuntimeHealth.Healthy;
                _started = false;
            }

            _logger.LogInformation("Aura runtime stopped.");
        }
        catch (OperationCanceledException)
        {
            lock (_gate)
            {
                _state = RuntimeState.Stopped;
                _health = RuntimeHealth.Degraded;
                _started = false;
            }

            _logger.LogWarning("Aura runtime shutdown was canceled.");
            throw;
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                _state = RuntimeState.Faulted;
                _health = RuntimeHealth.Faulted;
                _started = false;
            }

            _logger.LogCritical(ex, "Aura runtime failed during shutdown.");
            throw;
        }
    }
}
