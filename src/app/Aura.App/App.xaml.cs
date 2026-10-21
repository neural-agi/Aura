using Microsoft.Extensions.DependencyInjection;
using Aura.UI.Windowing;

namespace Aura.App;

public sealed partial class App : Microsoft.UI.Xaml.Application
{
    public App()
    {
        Bootstrap.BootstrapRuntime.Initialize();
    }

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        if (!Bootstrap.SingleInstanceActivation.RegisterOrActivateCurrentInstance(
            Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs()!))
        {
            Exit();
            return;
        }

        await Bootstrap.BootstrapRuntime.StartAsync();

        var host = Bootstrap.BootstrapRuntime.GetHost();
        var windowManager = host.Services.GetRequiredService<IWindowManager>();
        windowManager.ShutdownRequested += async (_, _) =>
        {
            await Bootstrap.BootstrapRuntime.ShutdownAsync();
            Exit();
        };
        var notificationAreaIcon = host.Services.GetRequiredService<Aura.UI.Interop.INotificationAreaIcon>();
        notificationAreaIcon.ExitRequested += (_, _) => windowManager.RequestApplicationExit();
        await windowManager.StartAsync();
    }
}
