using MediatR;
using OmniOps.Application.Dtos;
using OmniOps.Application.Queries;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Queries.Handlers;

public class GetFleetAnalyticsQueryHandler : IRequestHandler<GetFleetAnalyticsQuery, FleetAnalyticsDto>
{
    private readonly IFleetAnalyticsService _analytics;

    private readonly ITenantScope _scope;

    public GetFleetAnalyticsQueryHandler(IFleetAnalyticsService analytics, ITenantScope scope)
    {
        _analytics = analytics;
        _scope = scope;
    }

    public async Task<FleetAnalyticsDto> Handle(GetFleetAnalyticsQuery request, CancellationToken cancellationToken)
    {
        await _scope.EnsureFleetAsync(request.FleetId, cancellationToken);
        var r = await _analytics.GetFleetAnalyticsAsync(request.FleetId, request.FromUtc, request.ToUtc, cancellationToken);
        return new FleetAnalyticsDto(r.FleetId, r.VehicleCount, r.TotalDistanceKm, r.AvgSpeedKmh, r.IncidentCount, r.ActiveVehicles);
    }
}

public class GetDriverAnalyticsQueryHandler : IRequestHandler<GetDriverAnalyticsQuery, IReadOnlyList<DriverAnalyticsDto>>
{
    private readonly IFleetAnalyticsService _analytics;

    private readonly ITenantScope _scope;

    public GetDriverAnalyticsQueryHandler(IFleetAnalyticsService analytics, ITenantScope scope)
    {
        _analytics = analytics;
        _scope = scope;
    }

    public async Task<IReadOnlyList<DriverAnalyticsDto>> Handle(GetDriverAnalyticsQuery request, CancellationToken cancellationToken)
    {
        await _scope.EnsureFleetAsync(request.FleetId, cancellationToken);
        var items = await _analytics.GetDriverAnalyticsAsync(request.FleetId, request.FromUtc, request.ToUtc, cancellationToken);
        return items.Select(d => new DriverAnalyticsDto(d.DriverId, d.FullName, d.SafetyScore, d.TripCount)).ToList();
    }
}

public class GetOperationalAnalyticsQueryHandler : IRequestHandler<GetOperationalAnalyticsQuery, OperationalAnalyticsDto>
{
    private readonly IFleetAnalyticsService _analytics;

    private readonly ITenantScope _scope;

    public GetOperationalAnalyticsQueryHandler(IFleetAnalyticsService analytics, ITenantScope scope)
    {
        _analytics = analytics;
        _scope = scope;
    }

    public async Task<OperationalAnalyticsDto> Handle(GetOperationalAnalyticsQuery request, CancellationToken cancellationToken)
    {
        await _scope.EnsureFleetAsync(request.FleetId, cancellationToken);
        var r = await _analytics.GetOperationalAnalyticsAsync(request.FleetId, request.FromUtc, request.ToUtc, cancellationToken);
        return new OperationalAnalyticsDto(r.FleetId, r.ActiveTrips, r.CompletedTrips, r.OpenIncidents, r.TotalDepotCapacity);
    }
}
