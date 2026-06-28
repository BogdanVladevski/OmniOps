using MediatR;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;
using OmniOps.Core.Telemetry;

namespace OmniOps.Infrastructure.Handlers;

public class GetLatestTelemetryQueryHandler : IRequestHandler<GetLatestTelemetryQuery, VehicleTelemetry?>
{
    private readonly ITelemetryCacheService _cacheService;

    public GetLatestTelemetryQueryHandler(ITelemetryCacheService cacheService)
    {
        _cacheService = cacheService;
    }

    public async Task<VehicleTelemetry?> Handle(GetLatestTelemetryQuery request, CancellationToken cancellationToken)
    {
        
        return await _cacheService.GetLatestTelemetryAsync(request.VehicleId);
    }
}