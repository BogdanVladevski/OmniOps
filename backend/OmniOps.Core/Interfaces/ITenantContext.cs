namespace OmniOps.Core.Interfaces;

public interface ITenantContext
{
    Guid OrganizationId { get; }
    Guid? WorkspaceId { get; }
    string? UserId { get; }
    bool IsAuthenticated { get; }
}
