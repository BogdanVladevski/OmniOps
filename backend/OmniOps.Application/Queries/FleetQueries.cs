using MediatR;
using OmniOps.Application.Abstractions;
using OmniOps.Application.Dtos;
using OmniOps.Core.Authorization;
using OmniOps.Core.Domain;

namespace OmniOps.Application.Queries;

public record GetFleetsQuery() : IRequest<IReadOnlyList<FleetDto>>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

public record GetFleetStatisticsQuery(Guid FleetId) : IRequest<FleetStatisticsDto>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

public record GetVehiclesByFleetQuery(Guid FleetId) : IRequest<IReadOnlyList<VehicleDto>>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

public record GetGeofencesQuery(Guid? FleetId = null) : IRequest<IReadOnlyList<GeofenceDto>>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

public record GetStoredEventsQuery(
    DateTime FromUtc,
    DateTime ToUtc,
    string? EventType = null) : IRequest<IReadOnlyList<StoredEventDto>>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

public record GetTelemetryAggregationsQuery(
    string VehicleId,
    DateTime FromUtc,
    DateTime ToUtc,
    int BucketMinutes = 5) : IRequest<IReadOnlyList<TelemetryAggregationBucket>>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

public record GetFleetHeatmapQuery(
    Guid FleetId,
    DateTime FromUtc,
    DateTime ToUtc,
    double GridSizeDegrees = 0.01) : IRequest<IReadOnlyList<HeatmapBucket>>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

public record GetVehicleClustersQuery(
    Guid FleetId,
    double ClusterRadiusMeters = 500) : IRequest<IReadOnlyList<VehicleClusterDto>>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}
