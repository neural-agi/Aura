using Aura.UI.Interop;
using Aura.UI.Monitoring;
using Aura.UI.Theming;
using Aura.UI.Windowing;
using Microsoft.Extensions.DependencyInjection;

namespace Aura.UI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAuraUiShell(this IServiceCollection services)
    {
        services.AddSingleton<IWin32Interop, Win32Interop>();
        services.AddSingleton<IMonitorService, MonitorService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IWindowManager, WindowManager>();
        return services;
    }
}

