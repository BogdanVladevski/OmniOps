namespace OmniOps.Core.Interfaces;

/// <summary>
/// Ownership checks for the caller's organization. Each throws <see cref="KeyNotFoundException"/> when the item
/// does not exist <b>or belongs to another organization</b> — callers answer 404 either way, never 403.
/// </summary>
public interface ITenantScope
{
    Task EnsureFleetAsync(Guid fleetId, CancellationToken cancellationToken = default);
    Task EnsureVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default);
    Task EnsureDriverAsync(Guid driverId, CancellationToken cancellationToken = default);
    Task EnsureIncidentAsync(Guid incidentId, CancellationToken cancellationToken = default);

    /// <summary>Fleet of the caller's vehicle with this external id (unique within an organization's fleets).</summary>
    Task<Guid> FleetOfVehicleAsync(string externalId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> FleetIdsAsync(CancellationToken cancellationToken = default);
}
