using Microsoft.Extensions.DependencyInjection;
using Aura.UI.Windowing;

namespace Aura.App;

public sealed partial class App : Microsoft.UI.Xaml.Application
{
    public App()
    {
        InitializeComponent();
        Bootstrap.BootstrapRuntime.Initialize();
    }

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        await Bootstrap.BootstrapRuntime.StartAsync();

        var host = Bootstrap.BootstrapRuntime.GetHost();
        var windowManager = host.Services.GetRequiredService<IWindowManager>();
        windowManager.ShutdownRequested += async (_, _) => await Bootstrap.BootstrapRuntime.ShutdownAsync();
        await windowManager.StartAsync();
    }
}
