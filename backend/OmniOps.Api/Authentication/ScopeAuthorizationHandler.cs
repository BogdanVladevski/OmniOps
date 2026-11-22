using Microsoft.AspNetCore.Authorization;

namespace OmniOps.Api.Authentication;

/// <summary>Satisfied when the token carries any of the listed scopes.</summary>
public class ScopeRequirement : IAuthorizationRequirement
{
    public ScopeRequirement(params string[] scopes) => Scopes = scopes;

    public IReadOnlyList<string> Scopes { get; }
}

public class ScopeAuthorizationHandler : AuthorizationHandler<ScopeRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ScopeRequirement requirement)
    {
        var granted = context.User.FindAll("scope")
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (granted.Any(scope => requirement.Scopes.Contains(scope, StringComparer.Ordinal)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
