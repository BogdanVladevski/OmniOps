namespace OmniOps.Application.Dtos;

public record FleetAnalyticsDto(
    Guid FleetId, int VehicleCount, double TotalDistanceKm, double AvgSpeedKmh,
    int IncidentCount, int ActiveVehicles);

public record DriverAnalyticsDto(Guid DriverId, string FullName, double SafetyScore, int TripCount);

public record OperationalAnalyticsDto(
    Guid FleetId, int ActiveTrips, int CompletedTrips, int OpenIncidents, int TotalDepotCapacity);
