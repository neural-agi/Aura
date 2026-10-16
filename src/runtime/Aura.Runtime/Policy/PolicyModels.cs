namespace Aura.Runtime.Policy;

public enum PolicyDecision { Allowed, Denied, Restricted, Unknown }
public enum PolicyResource { SensitiveData, Surface, Retention, ExternalIntegration }

public sealed record PolicyRequest(
    PolicyResource Resource,
    string Operation,
    bool IsSensitive = false,
    bool AuthorizationKnown = true,
    bool IsAuthorized = false);
public sealed record PolicyResult(PolicyDecision Decision, string Reason)
{
    public bool IsAllowed => Decision == PolicyDecision.Allowed;
    public bool CanSurface => Decision == PolicyDecision.Allowed;
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
        if (!Enum.IsDefined(request.Resource))
        {
            return new(PolicyDecision.Denied, "Unknown policy resource is denied.");
        }

        if (string.IsNullOrWhiteSpace(request.Operation))
        {
            return new(PolicyDecision.Denied, "A policy operation is required.");
        }

        if (request.Resource == PolicyResource.SensitiveData || request.IsSensitive)
        {
            if (!request.AuthorizationKnown) return new(PolicyDecision.Unknown, "Sensitive-data authorization is unavailable.");
            return request.IsAuthorized
                ? new(PolicyDecision.Allowed, "Sensitive-data access is explicitly authorized.")
                : new(PolicyDecision.Restricted, "Sensitive-data access requires explicit authorization.");
        }

        if (request.Resource == PolicyResource.ExternalIntegration)
        {
            if (!request.AuthorizationKnown) return new(PolicyDecision.Unknown, "External authorization is unavailable.");
            return request.IsAuthorized
                ? new(PolicyDecision.Allowed, "External integration is explicitly authorized.")
                : new(PolicyDecision.Denied, "External integration is not authorized.");
        }

        if (request.Resource == PolicyResource.Surface && !request.AuthorizationKnown)
        {
            return new(PolicyDecision.Unknown, "Surface eligibility could not be authorized.");
        }

        if (request.Resource == PolicyResource.Retention && !request.AuthorizationKnown)
        {
            return new(PolicyDecision.Unknown, "Retention eligibility could not be authorized.");
        }

        return request.AuthorizationKnown
            ? new(PolicyDecision.Allowed, "Allowed by default policy.")
            : new(PolicyDecision.Unknown, "Policy evaluation is unavailable.");
    }
}
