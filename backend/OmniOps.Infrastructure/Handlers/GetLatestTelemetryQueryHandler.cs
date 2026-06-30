using MediatR;
using OmniOps.Core.DTOs;
using OmniOps.Core.Interfaces;
using OmniOps.Core.Telemetry;

namespace OmniOps.Infrastructure.Handlers;

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
        return telemetry != null ? TelemetryDto.FromEntity(telemetry) : null;
    }
}