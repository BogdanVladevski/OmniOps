namespace OmniOps.Core.Enums;

public enum IncidentType
{
    Overspeed,
    HarshBraking,
    HarshAcceleration,
    LongIdle,
    Crash,
    SensorFailure,
    UnauthorizedMovement,
    GeofenceBreach,
    TemperatureExcursion
}

public enum IncidentStatus
{
    Open,
    Assigned,
    Resolved,
    Closed
}

public enum IncidentSeverity
{
    Low,
    Medium,
    High,
    Critical
}
