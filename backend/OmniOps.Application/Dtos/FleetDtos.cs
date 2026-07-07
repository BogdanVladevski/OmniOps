namespace OmniOps.Application.Dtos;

public record FleetDto(Guid Id, string Name, string? Description, DateTime CreatedAtUtc)
{
    public static FleetDto FromEntity(Core.Entities.Fleet fleet) =>
        new(fleet.Id, fleet.Name, fleet.Description, fleet.CreatedAtUtc);
}

public record FleetStatisticsDto(
    Guid FleetId,
    int VehicleCount,
    int ActiveTripCount,
    int DriverCount,
    int DepotCount);

public record VehicleDto(
    Guid Id,
    Guid FleetId,
    string ExternalId,
    string? Vin,
    string? Registration,
    string? InsuranceProvider,
    DateTime? InsuranceExpiryUtc,
    string Status,
    Guid? AssignedDriverId)
{
    public static VehicleDto FromEntity(Core.Entities.Vehicle vehicle) =>
        new(
            vehicle.Id,
            vehicle.FleetId,
            vehicle.ExternalId,
            vehicle.Vin,
            vehicle.Registration,
            vehicle.InsuranceProvider,
            vehicle.InsuranceExpiryUtc,
            vehicle.Status.ToString(),
            vehicle.AssignedDriverId);
}

public record DriverDto(Guid Id, Guid FleetId, string FullName, string? LicenseNumber, double SafetyScore)
{
    public static DriverDto FromEntity(Core.Entities.Driver driver) =>
        new(driver.Id, driver.FleetId, driver.FullName, driver.LicenseNumber, driver.SafetyScore);
}

public record TripDto(
    Guid Id,
    Guid VehicleId,
    Guid? DriverId,
    string Status,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    string? Origin,
    string? Destination)
{
    public static TripDto FromEntity(Core.Entities.Trip trip) =>
        new(
            trip.Id,
            trip.VehicleId,
            trip.DriverId,
            trip.Status.ToString(),
            trip.StartedAtUtc,
            trip.CompletedAtUtc,
            trip.Origin,
            trip.Destination);
}

public record DepotDto(Guid Id, Guid FleetId, string Name, double Latitude, double Longitude, int Capacity)
{
    public static DepotDto FromEntity(Core.Entities.Depot depot) =>
        new(depot.Id, depot.FleetId, depot.Name, depot.Latitude, depot.Longitude, depot.Capacity);
}

public record GeofenceDto(
    Guid Id,
    Guid? FleetId,
    string Name,
    string ShapeType,
    double? CenterLatitude,
    double? CenterLongitude,
    double? RadiusMeters,
    string? PolygonCoordinatesJson,
    bool IsActive)
{
    public static GeofenceDto FromEntity(Core.Entities.Geofence geofence) =>
        new(
            geofence.Id,
            geofence.FleetId,
            geofence.Name,
            geofence.ShapeType.ToString(),
            geofence.CenterLatitude,
            geofence.CenterLongitude,
            geofence.RadiusMeters,
            geofence.PolygonCoordinatesJson,
            geofence.IsActive);
}

public record StoredEventDto(
    Guid Id,
    string EventType,
    int SchemaVersion,
    string AggregateType,
    string AggregateId,
    DateTime OccurredOnUtc);

public record VehicleClusterDto(string VehicleId, double Latitude, double Longitude, int ClusterSize);
