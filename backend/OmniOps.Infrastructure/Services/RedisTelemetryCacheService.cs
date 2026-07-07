using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Configuration;

namespace OmniOps.Infrastructure.Services;

public class RedisTelemetryCacheService : ITelemetryCacheService
{
    private readonly IDistributedCache _cache;
    private readonly CacheOptions _options;

    public RedisTelemetryCacheService(IDistributedCache cache, IOptions<CacheOptions> options)
    {
        _cache = cache;
        _options = options.Value;
    }

    public async Task SetLatestTelemetryAsync(VehicleTelemetry telemetry)
    {
        var cacheKey = $"telemetry:latest:{telemetry.VehicleId}";
        var jsonData = System.Text.Json.JsonSerializer.Serialize(telemetry);

        var cacheOptions = new DistributedCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(_options.SlidingExpirationMinutes),
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(_options.AbsoluteExpirationHours)
        };

        await _cache.SetStringAsync(cacheKey, jsonData, cacheOptions);
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