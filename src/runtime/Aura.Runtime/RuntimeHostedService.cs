using Microsoft.Extensions.Hosting;

namespace Aura.Runtime;

public sealed class RuntimeHostedService : IHostedService
{
    private readonly IRuntimeCore _runtimeCore;

    public RuntimeHostedService(IRuntimeCore runtimeCore)
    {
        _runtimeCore = runtimeCore;
    }

    public Task StartAsync(CancellationToken cancellationToken)
        => _runtimeCore.StartAsync(cancellationToken).AsTask();

    public Task StopAsync(CancellationToken cancellationToken)
        => _runtimeCore.StopAsync(cancellationToken).AsTask();
}

