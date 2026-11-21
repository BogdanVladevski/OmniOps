using OmniOps.Core.Entities;

namespace OmniOps.Core.Interfaces;

public interface IShipmentRepository
{
    /// <summary>In-transit shipment for the vehicle within the given fleet (vehicle ids are unique per fleet only).</summary>
    Task<Shipment?> GetActiveShipmentForVehicleAsync(
        Guid fleetId, string vehicleId, CancellationToken cancellationToken = default);
    /// <summary>Shipment by id, only if its fleet belongs to <paramref name="organizationId"/>; otherwise null.</summary>
    Task<Shipment?> GetByIdForOrganizationAsync(
        Guid shipmentId, Guid organizationId, CancellationToken cancellationToken = default);
}
