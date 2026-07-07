using MediatR;
using OmniOps.Application.Abstractions;
using OmniOps.Application.Dtos;
using OmniOps.Core.Authorization;

namespace OmniOps.Application.Commands;

public record CreateFleetCommand(string Name, string? Description) : IRequest<FleetDto>, IAuthorizedRequest, ITransactionalRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

public record CreateVehicleCommand(
    Guid FleetId,
    string ExternalId,
    string? Vin,
    string? Registration) : IRequest<VehicleDto>, IAuthorizedRequest, ITransactionalRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

public record CreateDriverCommand(
    Guid FleetId,
    string FullName,
    string? LicenseNumber) : IRequest<DriverDto>, IAuthorizedRequest, ITransactionalRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

public record AssignDriverToVehicleCommand(Guid VehicleId, Guid DriverId) : IRequest<VehicleDto>, IAuthorizedRequest, ITransactionalRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

public record CreateTripCommand(
    Guid VehicleId,
    Guid? DriverId,
    string? Origin,
    string? Destination) : IRequest<TripDto>, IAuthorizedRequest, ITransactionalRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

public record StartTripCommand(Guid TripId) : IRequest<TripDto>, IAuthorizedRequest, ITransactionalRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

public record CompleteTripCommand(Guid TripId) : IRequest<TripDto>, IAuthorizedRequest, ITransactionalRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

public record CreateDepotCommand(
    Guid FleetId,
    string Name,
    double Latitude,
    double Longitude,
    int Capacity) : IRequest<DepotDto>, IAuthorizedRequest, ITransactionalRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

public record CreateGeofenceCommand(
    Guid? FleetId,
    string Name,
    string ShapeType,
    double? CenterLatitude,
    double? CenterLongitude,
    double? RadiusMeters,
    string? PolygonCoordinatesJson) : IRequest<GeofenceDto>, IAuthorizedRequest, ITransactionalRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

public record ReplayEventsCommand(
    DateTime FromUtc,
    DateTime ToUtc,
    string? EventType = null) : IRequest<int>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleSimulate;
}
