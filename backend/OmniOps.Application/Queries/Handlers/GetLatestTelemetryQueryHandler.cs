using MediatR;
using OmniOps.Application.Dtos;
using OmniOps.Application.Queries;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Queries.Handlers;

public class GetLatestTelemetryQueryHandler : IRequestHandler<GetLatestTelemetryQuery, TelemetryDto?>
{
    private readonly ITelemetryCacheService _cacheService;

    public GetLatestTelemetryQueryHandler(ITelemetryCacheService cacheService)
    {
        _cacheService = cacheService;
    }

    public async Task<TelemetryDto?> Handle(GetLatestTelemetryQuery request, CancellationToken cancellationToken)
    {
        var telemetry = await _cacheService.GetLatestTelemetryAsync(request.VehicleId);
        return telemetry is not null ? TelemetryDto.FromEntity(telemetry) : null;
    }
}
