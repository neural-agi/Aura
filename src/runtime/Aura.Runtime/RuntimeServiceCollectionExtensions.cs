using Microsoft.Extensions.DependencyInjection;
using Aura.Runtime.Decision;

namespace Aura.Runtime;

public static class RuntimeServiceCollectionExtensions
{
    public static IServiceCollection AddAuraRuntime(this IServiceCollection services)
    {
        services.AddSingleton<IEventDispatcher, EventDispatcher>();
        services.AddSingleton<IRuntimeScheduler, RuntimeScheduler>();
        services.AddSingleton<IRuntimeCore, RuntimeCore>();
        services.AddSingleton<IStateMachine, StateMachine>();
        services.AddSingleton<Context.IContextEngine, Context.ContextEngine>();
        services.AddSingleton<Priority.IPriorityEngine, Priority.PriorityEngine>();
        services.AddSingleton<Policy.IPolicyEvaluator, Policy.PolicyEvaluator>();
        services.AddSingleton<Settings.ISettingsStore, Settings.JsonSettingsStore>();
        services.AddSingleton<IRuntimeDecisionPipeline, RuntimeDecisionPipeline>();
        services.AddHostedService<RuntimeHostedService>();
        return services;
    }
}

