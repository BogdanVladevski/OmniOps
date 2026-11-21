using OmniOps.Core.Domain;
using OmniOps.Core.Entities;

namespace OmniOps.Core.Interfaces;

public interface IVehicleRepository
{
    Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Vehicle?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Owners of vehicles with this external id. ExternalId is unique per fleet only, so more than
    /// one result means the id is ambiguous across fleets/tenants. Returns at most <paramref name="max"/>.
    /// </summary>
    Task<IReadOnlyList<VehicleTenant>> FindTenantsByExternalIdAsync(
        string externalId, int max, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fleet of the vehicle with this external id inside <paramref name="organizationId"/>, or null when the
    /// organization has no such vehicle or has it in more than one fleet.
    /// </summary>
    Task<Guid?> FindFleetIdInOrganizationAsync(
        string externalId, Guid organizationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Vehicle>> GetByFleetIdAsync(Guid fleetId, CancellationToken cancellationToken = default);
    Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken = default);
    Task AddMaintenanceRecordAsync(VehicleMaintenanceRecord record, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
