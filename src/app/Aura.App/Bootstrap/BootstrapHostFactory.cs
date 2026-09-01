using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Aura.Runtime;
using Aura.UI;
using Aura.UI.Windowing;
using Aura.UI.Surface;

namespace Aura.App.Bootstrap;

public static class BootstrapHostFactory
{
    public static IHost CreateHost()
    {
        var builder = Host.CreateDefaultBuilder();

        builder.ConfigureAppConfiguration((context, configuration) =>
        {
            configuration.Sources.Clear();
            configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            configuration.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
            configuration.AddEnvironmentVariables();
        });

        builder.UseSerilog((context, services, loggerConfiguration) =>
        {
            loggerConfiguration
                .MinimumLevel.Information()
                .Enrich.FromLogContext()
                .WriteTo.Console();
        });

        builder.ConfigureServices((context, services) =>
        {
            services.AddAuraRuntime();
            services.AddAuraUiShell();
            services.AddSingleton<IAppWindowFactory>(sp => new DelegateAppWindowFactory(() => new MainWindow(sp.GetRequiredService<ISurfaceView>())));
        });

        return builder.Build();
    }
}
