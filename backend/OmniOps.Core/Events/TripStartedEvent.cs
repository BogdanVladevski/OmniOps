using OmniOps.Core.Interfaces;

namespace OmniOps.Core.Events;

public class TripStartedEvent : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
    public Guid TripId { get; }
    public Guid VehicleId { get; }
    public Guid? DriverId { get; }

    public TripStartedEvent(Guid tripId, Guid vehicleId, Guid? driverId)
    {
        TripId = tripId;
        VehicleId = vehicleId;
        DriverId = driverId;
    }
}
