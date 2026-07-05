using OmniOps.Core.Entities;

namespace OmniOps.Core.Interfaces;

public interface IShipmentRepository
{
    Task<Shipment?> GetActiveShipmentForVehicleAsync(string vehicleId, CancellationToken cancellationToken = default);
}
