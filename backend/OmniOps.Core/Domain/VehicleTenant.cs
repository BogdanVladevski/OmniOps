namespace OmniOps.Core.Domain;

/// <summary>Tenant ownership (organization + fleet) of the vehicle that produced a telemetry packet.</summary>
public sealed record VehicleTenant(Guid OrganizationId, Guid FleetId)
{
    /// <summary>Reserved ids that match no real organization/fleet, so no tenant user or API key can read them.</summary>
    public static readonly Guid QuarantineOrganizationId = new("0e000000-0000-0000-0000-00000000000e");
    public static readonly Guid QuarantineFleetId = new("fe000000-0000-0000-0000-00000000000e");

    /// <summary>
    /// Quarantine attribution used when a packet cannot be matched to exactly one registered vehicle
    /// (unregistered, ambiguous across fleets, or lookup failure). Incidents are still recorded so no
    /// alert is lost, but they land outside every real tenant; each use is logged and counted
    /// (telemetry.tenant_unresolved) for operators to triage.
    /// </summary>
    public static readonly VehicleTenant Unattributed = new(QuarantineOrganizationId, QuarantineFleetId);

    public bool IsUnattributed => this == Unattributed;
}
