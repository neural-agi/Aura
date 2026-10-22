using Aura.Runtime.Media;
using Microsoft.Extensions.Hosting;

namespace Aura.Platform.Windows.Media;

internal sealed class MediaControlHostedService(IMediaControlService media) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => media.StartAsync(cancellationToken).AsTask();
    public Task StopAsync(CancellationToken cancellationToken) => media.StopAsync(cancellationToken).AsTask();
}
