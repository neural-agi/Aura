using Aura.UI.Interop;
using Aura.UI.Monitoring;
using Aura.UI.Theming;
using Aura.UI.Windowing;
using Aura.UI.Surface;
using Microsoft.Extensions.DependencyInjection;
using Aura.Runtime;

namespace Aura.UI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAuraUiShell(this IServiceCollection services)
    {
        services.AddSingleton<IWin32Interop, Win32Interop>();
        services.AddSingleton<IMonitorService, MonitorService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IWindowManager, WindowManager>();
        services.AddSingleton<ISurfaceView, AuraSurfaceView>();
        services.AddSingleton<ISurfaceManager, SurfaceManager>();
        services.AddSingleton<Interaction.IInteractionEngine, Interaction.InteractionEngine>();
        return services;
    }
}

