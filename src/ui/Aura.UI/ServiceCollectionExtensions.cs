using Aura.UI.Interop;
using Aura.UI.Monitoring;
using Aura.UI.Theming;
using Aura.UI.Windowing;
using Aura.UI.Surface;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Aura.Runtime;

namespace Aura.UI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAuraUiShell(this IServiceCollection services)
    {
        services.AddSingleton<IWin32Interop, Win32Interop>();
        services.AddSingleton<Win32ClipboardNativeApi>();
        services.AddSingleton<IClipboardNativeApi>(sp => sp.GetRequiredService<Win32ClipboardNativeApi>());
        services.AddSingleton<Aura.Runtime.Artifacts.IClipboardTextWriter>(sp => sp.GetRequiredService<Win32ClipboardNativeApi>());
        services.AddSingleton<IClipboardWindowListener, ClipboardWindowListener>();
        services.AddSingleton<INotificationAreaIcon, NotificationAreaIcon>();
        services.AddSingleton<Aura.Runtime.Artifacts.IClipboardArtifactPersistence, Aura.Runtime.Artifacts.JsonClipboardArtifactPersistence>();
        services.AddSingleton<Aura.Runtime.Artifacts.ClipboardSensitivityClassifier>();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<Aura.Runtime.Artifacts.ClipboardArtifactManager>();
        services.AddSingleton<Aura.Runtime.Artifacts.IClipboardArtifactManager>(sp => sp.GetRequiredService<Aura.Runtime.Artifacts.ClipboardArtifactManager>());
        services.AddSingleton<Microsoft.Extensions.Hosting.IHostedService>(sp => sp.GetRequiredService<Aura.Runtime.Artifacts.ClipboardArtifactManager>());
        services.AddSingleton<IMonitorService, MonitorService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IWindowManager, WindowManager>();
        services.AddSingleton<ISurfaceView, AuraSurfaceView>();
        services.AddSingleton<SurfaceManager>();
        services.AddSingleton<ISurfaceManager>(sp => sp.GetRequiredService<SurfaceManager>());
        services.Replace(ServiceDescriptor.Singleton<Aura.Runtime.Notifications.INotificationProjectionSink>(sp => sp.GetRequiredService<SurfaceManager>()));
        services.AddSingleton<Interaction.IInteractionEngine, Interaction.InteractionEngine>();
        return services;
    }
}

