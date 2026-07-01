using Microsoft.AspNetCore.Authorization;

namespace OmniOps.Api.Authentication;

public class ScopeRequirement : IAuthorizationRequirement
{
    public ScopeRequirement(string scope) => Scope = scope;
    public string Scope { get; }
}

public class ScopeAuthorizationHandler : AuthorizationHandler<ScopeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ScopeRequirement requirement)
    {
        var scopeClaims = context.User.FindAll("scope");
        foreach (var claim in scopeClaims)
        {
            var scopes = claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (scopes.Contains(requirement.Scope, StringComparer.Ordinal))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }
        }

        return Task.CompletedTask;
    }
}
