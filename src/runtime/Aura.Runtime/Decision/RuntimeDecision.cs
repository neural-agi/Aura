using Aura.Runtime.Context;
using Aura.Runtime.Policy;
using Aura.Runtime.Priority;
using Aura.Runtime.Notifications;

namespace Aura.Runtime.Decision;

public sealed record RuntimeEvaluationInput(
    ContextInput Context,
    PriorityInput Priority,
    PolicyRequest Policy,
    AuraNotification? Notification = null);
public sealed record RuntimeDecision(
    ContextSnapshot Context,
    PriorityDecision Priority,
    PolicyResult Policy,
    bool StateTransitionRequested,
    bool IsAuthorizedForSurface = false);

public abstract record RuntimeEvaluationResult
{
    private RuntimeEvaluationResult() { }

    public sealed record Success(RuntimeDecision Decision) : RuntimeEvaluationResult;
    public sealed record FailureResult(Exception Error) : RuntimeEvaluationResult;
}
public sealed record RuntimeEvaluationRequestedEvent(
    DateTimeOffset OccurredAtUtc,
    RuntimeEvaluationInput Input) : RuntimeEvent(OccurredAtUtc);

public interface IRuntimeDecisionPipeline
{
    RuntimeDecision Evaluate(RuntimeEvaluationInput input);
}

public sealed class RuntimeDecisionPipeline(
    IContextEngine contextEngine,
    IPriorityEngine priorityEngine,
    IPolicyEvaluator policyEvaluator,
    Settings.ISettingsStore settingsStore,
    IStateMachine stateMachine) : IRuntimeDecisionPipeline
{

    public RuntimeDecision Evaluate(RuntimeEvaluationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var context = contextEngine.Evaluate(input.Context);
        var policy = policyEvaluator.Evaluate(input.Policy);
        var settings = settingsStore.Current;
        var isConfiguredPrioritySource = settings.EffectivePrioritySources.Contains(input.Priority.Id);
        var settingsAllowSensitive = !input.Policy.IsSensitive
            && input.Policy.Resource != PolicyResource.SensitiveData || settings.AllowSensitiveData;

        var constrained = input.Priority with
        {
            Context = context,
            IsUserDeclaredPriority = input.Priority.IsUserDeclaredPriority || isConfiguredPrioritySource,
            PolicyAllowed = policy.CanSurface && settings.SurfaceEnabled && settingsAllowSensitive
        };
        var priority = priorityEngine.Evaluate(constrained);
        var authorizedForSurface = priority.ShouldSurface && policy.CanSurface && settings.SurfaceEnabled && settingsAllowSensitive;
        var currentStateAllowsReplacement = stateMachine.Current is AuraState.Surfacing or AuraState.Compact
            or AuraState.Expanded or AuraState.Interactive;
        var requested = authorizedForSurface && stateMachine.Current == AuraState.Idle
            && stateMachine.TryTransition(AuraState.Surfacing);
        return new RuntimeDecision(context, priority, policy, requested,
            authorizedForSurface && (requested || currentStateAllowsReplacement));
    }
}
