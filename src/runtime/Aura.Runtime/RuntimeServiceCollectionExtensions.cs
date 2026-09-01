using Microsoft.Extensions.DependencyInjection;

namespace Aura.Runtime;

public static class RuntimeServiceCollectionExtensions
{
    public static IServiceCollection AddAuraRuntime(this IServiceCollection services)
    {
        services.AddSingleton<IEventDispatcher, EventDispatcher>();
        services.AddSingleton<IRuntimeScheduler, RuntimeScheduler>();
        services.AddSingleton<IRuntimeCore, RuntimeCore>();
        services.AddSingleton<IStateMachine, StateMachine>();
        services.AddHostedService<RuntimeHostedService>();
        return services;
    }
}

