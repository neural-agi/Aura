namespace Aura.Runtime.Policy;

public enum PolicyDecision { Allowed, Denied, Restricted, Unknown }
public enum PolicyResource { SensitiveData, Surface, Retention, ExternalIntegration }

public sealed record PolicyRequest(PolicyResource Resource, string Operation, bool IsSensitive = false);
public sealed record PolicyResult(PolicyDecision Decision, string Reason)
{
    public bool IsAllowed => Decision == PolicyDecision.Allowed;
}

public interface IPolicyEvaluator
{
    PolicyResult Evaluate(PolicyRequest request);
}

public sealed class PolicyEvaluator : IPolicyEvaluator
{
    public PolicyResult Evaluate(PolicyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.IsSensitive && request.Resource == PolicyResource.SensitiveData)
            return new(PolicyDecision.Restricted, "Sensitive data requires explicit authorization.");
        return request.Resource == PolicyResource.ExternalIntegration
            ? new(PolicyDecision.Unknown, "External authorization is unavailable.")
            : new(PolicyDecision.Allowed, "Allowed by default policy.");
    }
}
