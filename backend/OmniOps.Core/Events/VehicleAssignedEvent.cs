using OmniOps.Core.Interfaces;

namespace OmniOps.Core.Events;

public class VehicleAssignedEvent : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
    public Guid VehicleId { get; }
    public Guid? DriverId { get; }
    public string VehicleExternalId { get; }

    public VehicleAssignedEvent(Guid vehicleId, string vehicleExternalId, Guid? driverId = null)
    {
        VehicleId = vehicleId;
        VehicleExternalId = vehicleExternalId;
        DriverId = driverId;
    }
}
