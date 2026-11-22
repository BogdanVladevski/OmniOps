using MediatR;
using OmniOps.Application.Dtos;
using OmniOps.Application.Queries;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Queries.Handlers;

public class GetFleetsQueryHandler : IRequestHandler<GetFleetsQuery, IReadOnlyList<FleetDto>>
{
    private readonly IFleetRepository _fleetRepository;

    private readonly ITenantContext _tenant;

    public GetFleetsQueryHandler(IFleetRepository fleetRepository, ITenantContext tenant)
    {
        _fleetRepository = fleetRepository;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<FleetDto>> Handle(GetFleetsQuery request, CancellationToken cancellationToken)
    {
        var fleets = await _fleetRepository.GetAllAsync(cancellationToken);
        return fleets.Where(f => f.OrganizationId == _tenant.OrganizationId).Select(FleetDto.FromEntity).ToList();
    }
}

public class GetFleetStatisticsQueryHandler : IRequestHandler<GetFleetStatisticsQuery, FleetStatisticsDto>
{
    private readonly IFleetRepository _fleetRepository;
    private readonly IDriverRepository _driverRepository;
    private readonly IDepotRepository _depotRepository;

    private readonly ITenantScope _scope;

    public GetFleetStatisticsQueryHandler(
        IFleetRepository fleetRepository,
        IDriverRepository driverRepository,
        IDepotRepository depotRepository,
        ITenantScope scope)
    {
        _scope = scope;
        _fleetRepository = fleetRepository;
        _driverRepository = driverRepository;
        _depotRepository = depotRepository;
    }

    public async Task<FleetStatisticsDto> Handle(GetFleetStatisticsQuery request, CancellationToken cancellationToken)
    {
        await _scope.EnsureFleetAsync(request.FleetId, cancellationToken);
        var vehicleCount = await _fleetRepository.CountVehiclesAsync(request.FleetId, cancellationToken);
        var activeTrips = await _fleetRepository.CountActiveTripsAsync(request.FleetId, cancellationToken);
        var drivers = await _driverRepository.GetByFleetIdAsync(request.FleetId, cancellationToken);
        var depots = await _depotRepository.GetByFleetIdAsync(request.FleetId, cancellationToken);
        return new FleetStatisticsDto(request.FleetId, vehicleCount, activeTrips, drivers.Count, depots.Count);
    }
}

public class GetVehiclesByFleetQueryHandler : IRequestHandler<GetVehiclesByFleetQuery, IReadOnlyList<VehicleDto>>
{
    private readonly IVehicleRepository _vehicleRepository;

    private readonly ITenantScope _scope;

    public GetVehiclesByFleetQueryHandler(IVehicleRepository vehicleRepository, ITenantScope scope)
    {
        _vehicleRepository = vehicleRepository;
        _scope = scope;
    }

    public async Task<IReadOnlyList<VehicleDto>> Handle(GetVehiclesByFleetQuery request, CancellationToken cancellationToken)
    {
        await _scope.EnsureFleetAsync(request.FleetId, cancellationToken);
        var vehicles = await _vehicleRepository.GetByFleetIdAsync(request.FleetId, cancellationToken);
        return vehicles.Select(VehicleDto.FromEntity).ToList();
    }
}

public class GetGeofencesQueryHandler : IRequestHandler<GetGeofencesQuery, IReadOnlyList<GeofenceDto>>
{
    private readonly IGeofenceRepository _geofenceRepository;

    private readonly ITenantScope _scope;

    public GetGeofencesQueryHandler(IGeofenceRepository geofenceRepository, ITenantScope scope)
    {
        _geofenceRepository = geofenceRepository;
        _scope = scope;
    }

    public async Task<IReadOnlyList<GeofenceDto>> Handle(GetGeofencesQuery request, CancellationToken cancellationToken)
    {
        // Zones without a fleet belong to no organization, so they are never shown.
        if (request.FleetId is { } fleetId)
        {
            await _scope.EnsureFleetAsync(fleetId, cancellationToken);
        }

        var own = (await _scope.FleetIdsAsync(cancellationToken)).ToHashSet();
        var geofences = await _geofenceRepository.GetActiveAsync(request.FleetId, cancellationToken);
        return geofences.Where(g => g.FleetId is { } f && own.Contains(f)).Select(GeofenceDto.FromEntity).ToList();
    }
}

public class GetStoredEventsQueryHandler : IRequestHandler<GetStoredEventsQuery, IReadOnlyList<StoredEventDto>>
{
    private readonly IEventStore _eventStore;

    public GetStoredEventsQueryHandler(IEventStore eventStore) => _eventStore = eventStore;

    public async Task<IReadOnlyList<StoredEventDto>> Handle(GetStoredEventsQuery request, CancellationToken cancellationToken)
    {
        var events = await _eventStore.GetByTimeRangeAsync(
            request.FromUtc, request.ToUtc, request.EventType, cancellationToken);
        return events.Select(e => new StoredEventDto(
            e.Id, e.EventType, e.SchemaVersion, e.AggregateType, e.AggregateId, e.OccurredOnUtc)).ToList();
    }
}

public class GetTelemetryAggregationsQueryHandler
    : IRequestHandler<GetTelemetryAggregationsQuery, IReadOnlyList<Core.Domain.TelemetryAggregationBucket>>
{
    private readonly ITelemetryRepository _telemetryRepository;

    private readonly ITenantScope _scope;

    public GetTelemetryAggregationsQueryHandler(ITelemetryRepository telemetryRepository, ITenantScope scope)
    {
        _telemetryRepository = telemetryRepository;
        _scope = scope;
    }

    public async Task<IReadOnlyList<Core.Domain.TelemetryAggregationBucket>> Handle(
        GetTelemetryAggregationsQuery request,
        CancellationToken cancellationToken)
    {
        var fleetId = await _scope.FleetOfVehicleAsync(request.VehicleId, cancellationToken);
        return await _telemetryRepository.GetAggregationsAsync(
            fleetId, request.VehicleId, request.FromUtc, request.ToUtc, request.BucketMinutes, cancellationToken);
    }
}

public class GetFleetHeatmapQueryHandler
    : IRequestHandler<GetFleetHeatmapQuery, IReadOnlyList<Core.Domain.HeatmapBucket>>
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly ITelemetryRepository _telemetryRepository;

    private readonly ITenantScope _scope;

    public GetFleetHeatmapQueryHandler(
        IVehicleRepository vehicleRepository,
        ITelemetryRepository telemetryRepository,
        ITenantScope scope)
    {
        _scope = scope;
        _vehicleRepository = vehicleRepository;
        _telemetryRepository = telemetryRepository;
    }

    public async Task<IReadOnlyList<Core.Domain.HeatmapBucket>> Handle(
        GetFleetHeatmapQuery request,
        CancellationToken cancellationToken)
    {
        await _scope.EnsureFleetAsync(request.FleetId, cancellationToken);
        var vehicles = await _vehicleRepository.GetByFleetIdAsync(request.FleetId, cancellationToken);
        var vehicleIds = vehicles.Select(v => v.ExternalId).ToList();
        return await _telemetryRepository.GetHeatmapBucketsAsync(
            request.FleetId,
            vehicleIds, request.FromUtc, request.ToUtc, request.GridSizeDegrees, cancellationToken);
    }
}

public class GetVehicleClustersQueryHandler : IRequestHandler<GetVehicleClustersQuery, IReadOnlyList<VehicleClusterDto>>
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly ITelemetryCacheService _cacheService;

    private readonly ITenantScope _scope;

    public GetVehicleClustersQueryHandler(
        IVehicleRepository vehicleRepository,
        ITelemetryCacheService cacheService,
        ITenantScope scope)
    {
        _scope = scope;
        _vehicleRepository = vehicleRepository;
        _cacheService = cacheService;
    }

    public async Task<IReadOnlyList<VehicleClusterDto>> Handle(
        GetVehicleClustersQuery request,
        CancellationToken cancellationToken)
    {
        await _scope.EnsureFleetAsync(request.FleetId, cancellationToken);
        var vehicles = await _vehicleRepository.GetByFleetIdAsync(request.FleetId, cancellationToken);
        var points = new List<(string VehicleId, double Lat, double Lon)>();

        foreach (var vehicle in vehicles)
        {
            var latest = await _cacheService.GetLatestTelemetryAsync(request.FleetId, vehicle.ExternalId);
            if (latest is not null)
            {
                points.Add((vehicle.ExternalId, latest.Latitude, latest.Longitude));
            }
        }

        var clusters = new List<VehicleClusterDto>();
        var visited = new HashSet<string>();

        foreach (var point in points)
        {
            if (visited.Contains(point.VehicleId))
            {
                continue;
            }

            var members = points.Where(p =>
                HaversineMeters(point.Lat, point.Lon, p.Lat, p.Lon) <= request.ClusterRadiusMeters).ToList();

            foreach (var member in members)
            {
                visited.Add(member.VehicleId);
            }

            var centerLat = members.Average(m => m.Lat);
            var centerLon = members.Average(m => m.Lon);
            clusters.Add(new VehicleClusterDto(point.VehicleId, centerLat, centerLon, members.Count));
        }

        return clusters;
    }

    private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadius = 6371000;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
            * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return earthRadius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
