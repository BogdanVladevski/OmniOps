namespace OmniOps.Core.Domain;

public record FleetAnalyticsResult(
    Guid FleetId, int VehicleCount, double TotalDistanceKm, double AvgSpeedKmh,
    int IncidentCount, int ActiveVehicles);

public record DriverAnalyticsResult(Guid DriverId, string FullName, double SafetyScore, int TripCount);

public record OperationalAnalyticsResult(
    Guid FleetId, int ActiveTrips, int CompletedTrips, int OpenIncidents, int TotalDepotCapacity);
