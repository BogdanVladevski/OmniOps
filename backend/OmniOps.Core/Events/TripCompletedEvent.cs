using OmniOps.Core.Interfaces;

namespace OmniOps.Core.Events;

public class TripCompletedEvent : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
    public Guid TripId { get; }
    public Guid VehicleId { get; }

    public TripCompletedEvent(Guid tripId, Guid vehicleId)
    {
        TripId = tripId;
        VehicleId = vehicleId;
    }
}
