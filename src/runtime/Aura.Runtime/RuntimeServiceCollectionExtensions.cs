using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Aura.Runtime.Decision;
using Aura.Runtime.Notifications;
using Aura.Runtime.Artifacts;

namespace Aura.Runtime;

public static class RuntimeServiceCollectionExtensions
{
    public static IServiceCollection AddAuraRuntime(this IServiceCollection services)
    {
        services.AddSingleton<IEventDispatcher, EventDispatcher>();
        services.AddSingleton<IRuntimeScheduler, RuntimeScheduler>();
        services.TryAddSingleton<INotificationProjectionSink, NullNotificationProjectionSink>();
        services.TryAddSingleton<INotificationProvider, UnavailableNotificationProvider>();
        services.TryAddSingleton<NotificationIdentitySet>();
        services.TryAddSingleton<NotificationIngestionService>();

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

