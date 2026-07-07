using OmniOps.Core.Enums;
using OmniOps.Core.Interfaces;

namespace OmniOps.Core.Events;

public class GeofenceBreachedEvent : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
    public Guid GeofenceId { get; }
    public string VehicleId { get; }
    public GeofenceBreachType BreachType { get; }

    public GeofenceBreachedEvent(Guid geofenceId, string vehicleId, GeofenceBreachType breachType)
    {
        GeofenceId = geofenceId;
        VehicleId = vehicleId;
        BreachType = breachType;
    }
}
