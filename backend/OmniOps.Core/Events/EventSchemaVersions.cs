namespace OmniOps.Core.Events;

/// <summary>
/// Central registry of domain-event schema versions. Bump when breaking payload shape changes.
/// </summary>
public static class EventSchemaVersions
{
    public const int TelemetryReceived = 1;
    public const int AnomalyDetected = 1;
    public const int VehicleAssigned = 1;
    public const int TripStarted = 1;
    public const int TripCompleted = 1;
    public const int GeofenceBreached = 1;

    public static int Resolve(string eventTypeName) => eventTypeName switch
    {
        nameof(TelemetryReceivedEvent) => TelemetryReceived,
        nameof(AnomalyDetectedEvent) => AnomalyDetected,
        nameof(VehicleAssignedEvent) => VehicleAssigned,
        nameof(TripStartedEvent) => TripStarted,
        nameof(TripCompletedEvent) => TripCompleted,
        nameof(GeofenceBreachedEvent) => GeofenceBreached,
        _ => 1
    };
}
