using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;

namespace OmniOps.Infrastructure.Services;

public class RedisTelemetryCacheService : ITelemetryCacheService
{
    private readonly IDistributedCache _cache;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

    public RedisTelemetryCacheService(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task SetLatestTelemetryAsync(VehicleTelemetry telemetry)
    {
        var cacheKey = $"telemetry:latest:{telemetry.VehicleId}";
        var jsonData = JsonSerializer.Serialize(telemetry);

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheDuration
        };

        await _cache.SetStringAsync(cacheKey, jsonData, options);
    }

    public async Task<VehicleTelemetry?> GetLatestTelemetryAsync(string vehicleId)
    {
        var cacheKey = $"telemetry:latest:{vehicleId}";
        var jsonData = await _cache.GetStringAsync(cacheKey);

        if (string.IsNullOrEmpty(jsonData))
            return null;

        return JsonSerializer.Deserialize<VehicleTelemetry>(jsonData);
    }
}
