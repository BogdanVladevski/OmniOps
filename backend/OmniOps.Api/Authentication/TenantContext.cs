using System.Security.Claims;
using Microsoft.Extensions.Options;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Configuration;

namespace OmniOps.Api.Authentication;

/// <summary>
/// The caller's organization, from their sign-in. With authentication on, a request without an
/// organization is refused — it never silently becomes the demo organization.
/// </summary>
public class TenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly bool _requireAuthentication;

    public TenantContext(IHttpContextAccessor httpContextAccessor, IOptions<JwtOptions> jwtOptions)
    {
        _httpContextAccessor = httpContextAccessor;
        _requireAuthentication = jwtOptions.Value.RequireAuthentication;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public Guid OrganizationId
    {
        get
        {
            if (Guid.TryParse(User?.FindFirstValue("org_id"), out var id))
            {
                return id;
            }

            return _requireAuthentication
                ? throw new OmniOps.Core.Exceptions.AuthenticationFailedException("Sign in to continue.")
                : TenantSeed.DefaultOrganizationId; // local development with authentication off
        }
    }

    public Guid? WorkspaceId =>
        Guid.TryParse(User?.FindFirstValue("workspace_id"), out var id)
            ? id
            : _requireAuthentication ? null : TenantSeed.DefaultWorkspaceId;

    public string? UserId =>
        User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User?.FindFirstValue("sub");

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public Guid? DriverId => Guid.TryParse(User?.FindFirstValue("driver_id"), out var id) ? id : null;
}
