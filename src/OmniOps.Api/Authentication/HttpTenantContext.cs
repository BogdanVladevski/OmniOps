using Microsoft.AspNetCore.Authorization;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Domain.Common;

namespace OmniOps.Api.Authentication;

/// <summary>The caller's company and identity, from their access token.</summary>
internal sealed class HttpTenantContext(IHttpContextAccessor http) : ITenantContext
{
    private System.Security.Claims.ClaimsPrincipal? User => http.HttpContext?.User;

    public Guid? CurrentOrganizationId => User.GuidClaim(OmniClaims.Organization);

    public Guid OrganizationId => CurrentOrganizationId ?? throw new AuthenticationFailedException("Sign in to continue.");

    public Guid? UserId => User.GuidClaim(OmniClaims.Subject);

    public Guid? DriverId => User.GuidClaim(OmniClaims.Driver);
}

/// <summary>Enforces a message's policy against the signed-in user (defence in depth behind the endpoint's own check).</summary>
internal sealed class HttpPolicyEnforcer(IAuthorizationService authorization, IHttpContextAccessor http) : IPolicyEnforcer
{
    public async Task EnforceAsync(string policy, CancellationToken cancellationToken = default)
    {
        var user = http.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            throw new AuthenticationFailedException("Sign in to continue.");
        }

        if (!(await authorization.AuthorizeAsync(user, policy)).Succeeded)
        {
            throw new UnauthorizedAccessException("You do not have access to this.");
        }
    }
}
