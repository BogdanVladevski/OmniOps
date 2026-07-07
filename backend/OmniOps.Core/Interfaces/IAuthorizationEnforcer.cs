namespace OmniOps.Core.Interfaces;

public interface IAuthorizationEnforcer
{
    Task EnforcePolicyAsync(string policyName, CancellationToken cancellationToken = default);
}
