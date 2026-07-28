using Microsoft.Extensions.Hosting;
using Serilog;

namespace Aura.App.Bootstrap;

public static class BootstrapRuntime
{
    private static IHost? _host;

    public static void Initialize()
    {
        _host ??= BootstrapHostFactory.CreateHost();
    }

    public static async Task StartAsync()
    {
        Initialize();

        if (_host is not null)
        {
            await _host.StartAsync();
        }
    }

    public static async Task ShutdownAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
            _host = null;
        }

        Log.CloseAndFlush();
    }

    public static IHost GetHost()
    {
        Initialize();
        return _host!;
    }
}

