using System.Security.Claims;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;

namespace OmniOps.Api.Authentication;

public class TenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantContext(IHttpContextAccessor httpContextAccessor) =>
        _httpContextAccessor = httpContextAccessor;

    public Guid OrganizationId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirstValue("org_id");
            return Guid.TryParse(claim, out var id) ? id : TenantSeed.DefaultOrganizationId;
        }
    }

    public Guid? WorkspaceId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirstValue("workspace_id");
            return Guid.TryParse(claim, out var id) ? id : TenantSeed.DefaultWorkspaceId;
        }
    }

    public string? UserId =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Name)
        ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue("sub");

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;
}
