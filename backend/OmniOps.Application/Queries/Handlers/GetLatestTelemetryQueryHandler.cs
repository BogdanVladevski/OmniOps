using MediatR;
using OmniOps.Application.Dtos;
using OmniOps.Application.Queries;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Queries.Handlers;

public class GetLatestTelemetryQueryHandler : IRequestHandler<GetLatestTelemetryQuery, TelemetryDto?>
{
    private readonly ITelemetryCacheService _cacheService;
    private readonly ITenantScope _scope;

    public GetLatestTelemetryQueryHandler(ITelemetryCacheService cacheService, ITenantScope scope)
    {
        _cacheService = cacheService;
        _scope = scope;
    }

    public async Task<TelemetryDto?> Handle(GetLatestTelemetryQuery request, CancellationToken cancellationToken)
    {
        // Another organization's vehicle with the same id is "not found".
        Guid fleetId;
        try
        {
            fleetId = await _scope.FleetOfVehicleAsync(request.VehicleId, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }

        var telemetry = await _cacheService.GetLatestTelemetryAsync(fleetId, request.VehicleId);
        return telemetry is not null ? TelemetryDto.FromEntity(telemetry) : null;
    }
}
