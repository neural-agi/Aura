using Aura.Platform.Windows.Media;
using Aura.Runtime.Media;
using Microsoft.Extensions.DependencyInjection;

namespace Aura.Platform.Windows;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAuraWindowsMedia(this IServiceCollection services)
    {
        services.AddSingleton<IMediaPlatformAdapter, WindowsMediaSessionAdapter>();
        services.AddSingleton<ISystemVolumeAdapter, WindowsSystemVolumeAdapter>();
        services.AddSingleton<MediaControlService>();
        services.AddSingleton<IMediaControlService>(sp => sp.GetRequiredService<MediaControlService>());
        services.AddSingleton<Microsoft.Extensions.Hosting.IHostedService>(sp => new MediaControlHostedService(sp.GetRequiredService<MediaControlService>()));
        return services;
    }
}
