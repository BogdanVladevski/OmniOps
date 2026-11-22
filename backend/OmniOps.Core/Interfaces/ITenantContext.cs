namespace OmniOps.Core.Interfaces;

public interface ITenantContext
{
    Guid OrganizationId { get; }
    Guid? WorkspaceId { get; }
    string? UserId { get; }
    bool IsAuthenticated { get; }

    /// <summary>Set for driver sign-ins: the driver may only see and act on this driver's jobs.</summary>
    Guid? DriverId { get; }
}
