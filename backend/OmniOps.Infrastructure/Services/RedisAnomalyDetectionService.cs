using System.Text.Json;
using Microsoft.Extensions.Logging;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;
using StackExchange.Redis;

namespace OmniOps.Infrastructure.Services;

public class RedisAnomalyDetectionService : IAnomalyDetectionService
{
    private readonly IConnectionMultiplexer _redisConnection;
    private readonly ILogger<RedisAnomalyDetectionService> _logger;

    private const int MaxWindowPoints = 30;
    private static readonly TimeSpan WindowDuration = TimeSpan.FromSeconds(30);

    public RedisAnomalyDetectionService(
        IConnectionMultiplexer redisConnection,
        ILogger<RedisAnomalyDetectionService> logger)
    {
        _redisConnection = redisConnection;
        _logger = logger;
    }

    public async Task<bool> AnalyzeTelemetryAsync(
        VehicleTelemetry telemetry,
        CancellationToken cancellationToken = default)
    {
        var db = _redisConnection.GetDatabase();
        var historyKey = $"vehicle:history:{telemetry.VehicleId}";

        try
        {
            var currentPoint = new TelemetryWindowPoint
            {
                Speed = telemetry.Speed,
                FuelLevel = telemetry.FuelLevel,
                EngineTemperature = telemetry.EngineTemperature,
                Timestamp = telemetry.Timestamp
            };

            await db.ListLeftPushAsync(historyKey, JsonSerializer.Serialize(currentPoint));
            await db.ListTrimAsync(historyKey, 0, MaxWindowPoints - 1);
            await db.KeyExpireAsync(historyKey, WindowDuration);

            var windowPayloads = await db.ListRangeAsync(historyKey, 0, MaxWindowPoints - 1);
            var windowPoints = windowPayloads
                .Select(p => JsonSerializer.Deserialize<TelemetryWindowPoint>(p.ToString()))
                .Where(p => p is not null)
                .Cast<TelemetryWindowPoint>()
                .Where(p => telemetry.Timestamp - p.Timestamp <= WindowDuration)
                .OrderBy(p => p.Timestamp)
                .ToList();

            if (windowPoints.Count < 2)
            {
                return false;
            }

            var oldest = windowPoints.First();
            var newest = windowPoints.Last();

            var fuelDropRate = oldest.FuelLevel - newest.FuelLevel;
            var engineTempRise = newest.EngineTemperature - oldest.EngineTemperature;
            var speedDrop = oldest.Speed - newest.Speed;

            var compoundingFuelDrop = fuelDropRate >= 5.0;
            var surgingEngineThermal = engineTempRise >= 8.0 && newest.EngineTemperature > 95.0;
            var suddenDeceleration = speedDrop >= 20.0;

            if (compoundingFuelDrop && surgingEngineThermal)
            {
                _logger.LogWarning(
                    "Compound anomaly detected for vehicle {VehicleId}. FuelDrop={FuelDrop}%, TempRise={TempRise}°C, SpeedDrop={SpeedDrop} km/h",
                    telemetry.VehicleId, fuelDropRate, engineTempRise, speedDrop);
                return true;
            }

            if (suddenDeceleration && newest.EngineTemperature > 100.0)
            {
                _logger.LogWarning(
                    "Critical anomaly detected for vehicle {VehicleId}. Sudden deceleration with engine overheat",
                    telemetry.VehicleId);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Anomaly detection error for vehicle {VehicleId}",
                telemetry.VehicleId);
        }

        return false;
    }

    private class TelemetryWindowPoint
    {
        public double Speed { get; set; }
        public double FuelLevel { get; set; }
        public double EngineTemperature { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
