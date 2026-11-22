using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Configuration;

namespace OmniOps.Infrastructure.Services;

public class TelemetryCacheService : ITelemetryCacheService
{
    private readonly IDistributedCache _cache;
    private readonly CacheOptions _options;

    public TelemetryCacheService(IDistributedCache cache, IOptions<CacheOptions> options)
    {
        _cache = cache;
        _options = options.Value;
    }

    public async Task SetLatestTelemetryAsync(VehicleTelemetry telemetry)
    {
        // Unattributed packets have no fleet and are not cached: nobody may read them.
        if (telemetry.FleetId is not { } fleetId)
        {
            return;
        }

        var cacheKey = Key(fleetId, telemetry.VehicleId);
        var jsonData = System.Text.Json.JsonSerializer.Serialize(telemetry);

        var cacheOptions = new DistributedCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(_options.SlidingExpirationMinutes),
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(_options.AbsoluteExpirationHours)
        };

        await _cache.SetStringAsync(cacheKey, jsonData, cacheOptions);
    }

    public async Task<VehicleTelemetry?> GetLatestTelemetryAsync(Guid fleetId, string vehicleId)
    {
        var cacheKey = Key(fleetId, vehicleId);
        var jsonData = await _cache.GetStringAsync(cacheKey);

        if (string.IsNullOrEmpty(jsonData))
            return null;

        return JsonSerializer.Deserialize<VehicleTelemetry>(jsonData);
    }

    // External ids repeat across fleets, so the fleet is part of the key.
    private static string Key(Guid fleetId, string vehicleId) => $"telemetry:latest:{fleetId:N}:{vehicleId}";
}
