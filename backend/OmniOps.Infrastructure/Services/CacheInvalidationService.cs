using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using OmniOps.Core.Interfaces;

namespace OmniOps.Infrastructure.Services;

public class CacheInvalidationService : ICacheInvalidationService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<CacheInvalidationService> _logger;

    public CacheInvalidationService(IDistributedCache cache, ILogger<CacheInvalidationService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task InvalidateVehicleTelemetryAsync(string vehicleId, CancellationToken cancellationToken = default)
    {
        await _cache.RemoveAsync($"telemetry:latest:{vehicleId}", cancellationToken);
        _logger.LogDebug("Invalidated telemetry cache for vehicle {VehicleId}", vehicleId);
    }

    public async Task InvalidateFleetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        await _cache.RemoveAsync("fleet:snapshot", cancellationToken);
        _logger.LogDebug("Invalidated fleet snapshot cache");
    }
}
