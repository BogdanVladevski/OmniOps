using System.Text.Json;
using Microsoft.Extensions.Logging;
using OmniOps.Core.Domain;
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

    // Excursion tracking keys expire after 24 h of inactivity (no telemetry for that vehicle).
    private static readonly TimeSpan ExcursionKeyTtl = TimeSpan.FromHours(24);

    public RedisAnomalyDetectionService(
        IConnectionMultiplexer redisConnection,
        ILogger<RedisAnomalyDetectionService> logger)
    {
        _redisConnection = redisConnection;
        _logger = logger;
    }

    public async Task<AnomalyAnalysis> AnalyzeTelemetryAsync(
        VehicleTelemetry telemetry,
        Shipment? shipment = null,
        CancellationToken cancellationToken = default)
    {
        var db = _redisConnection.GetDatabase();
        var historyKey = $"vehicle:history:{telemetry.VehicleId}";
        var excursionKey = $"shipment:excursion:{telemetry.VehicleId}";

        try
        {
            var currentPoint = new TelemetryWindowPoint
            {
                CargoTemperature = telemetry.EngineTemperature,
                FuelLevel = telemetry.FuelLevel,
                Speed = telemetry.Speed,
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

            // Update excursion duration for the active shipment.
            var excursionDurationSeconds = await UpdateExcursionDurationAsync(
                db, excursionKey, telemetry, shipment);

            if (windowPoints.Count < 2)
            {
                return new AnomalyAnalysis(false, excursionDurationSeconds);
            }

            var oldest = windowPoints.First();
            var newest = windowPoints.Last();

            // Temperature excursion: sustained cargo temp rise beyond the shipment's safe ceiling.
            var tempRise = newest.CargoTemperature - oldest.CargoTemperature;
            var cargoOverheat = tempRise >= 8.0 && newest.CargoTemperature > 95.0;

            // Sudden deceleration paired with a cargo temperature spike signals possible cold-chain break.
            var speedDrop = oldest.Speed - newest.Speed;
            var suddenStop = speedDrop >= 20.0 && newest.CargoTemperature > 100.0;

            // Hard excursion rule: temp has been outside the shipment's safe range for this window.
            var shipmentExcursion = shipment is not null
                && (newest.CargoTemperature > shipment.MaxSafeTempCelsius
                    || newest.CargoTemperature < shipment.MinSafeTempCelsius);

            bool isAnomaly;

            if (shipmentExcursion && excursionDurationSeconds > 0)
            {
                _logger.LogWarning(
                    "Temperature excursion detected for vehicle {VehicleId} carrying {Product} batch {Batch}. " +
                    "CargoTemp={CargoTemp}°C (safe range {Min}–{Max}°C), ExcursionDuration={Duration}s",
                    telemetry.VehicleId, shipment!.ProductName, shipment.BatchNumber,
                    newest.CargoTemperature, shipment.MinSafeTempCelsius, shipment.MaxSafeTempCelsius,
                    excursionDurationSeconds);
                isAnomaly = true;
            }
            else if (cargoOverheat)
            {
                _logger.LogWarning(
                    "Cargo temperature surge detected for vehicle {VehicleId}. " +
                    "TempRise={TempRise}°C, CurrentTemp={CargoTemp}°C",
                    telemetry.VehicleId, tempRise, newest.CargoTemperature);
                isAnomaly = true;
            }
            else if (suddenStop)
            {
                _logger.LogWarning(
                    "Sudden stop with elevated cargo temperature for vehicle {VehicleId}. " +
                    "SpeedDrop={SpeedDrop} km/h, CargoTemp={CargoTemp}°C",
                    telemetry.VehicleId, speedDrop, newest.CargoTemperature);
                isAnomaly = true;
            }
            else
            {
                isAnomaly = false;
            }

            return new AnomalyAnalysis(isAnomaly, excursionDurationSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Temperature excursion analysis error for vehicle {VehicleId}",
                telemetry.VehicleId);
        }

        return new AnomalyAnalysis(false);
    }

    /// <summary>
    /// Tracks consecutive seconds that cargo temperature has been outside the shipment's safe range.
    /// Stores the excursion start timestamp in Redis; resets when temperature returns to range.
    /// </summary>
    private static async Task<int> UpdateExcursionDurationAsync(
        IDatabase db,
        string excursionKey,
        VehicleTelemetry telemetry,
        Shipment? shipment)
    {
        if (shipment is null)
        {
            return 0;
        }

        var cargoTemp = telemetry.EngineTemperature;
        var inSafeRange = cargoTemp >= shipment.MinSafeTempCelsius
                          && cargoTemp <= shipment.MaxSafeTempCelsius;

        if (inSafeRange)
        {
            await db.KeyDeleteAsync(excursionKey);
            return 0;
        }

        var existing = await db.StringGetAsync(excursionKey);
        DateTime excursionStartUtc;

        if (existing.IsNullOrEmpty)
        {
            excursionStartUtc = telemetry.Timestamp;
            await db.StringSetAsync(
                excursionKey,
                excursionStartUtc.ToString("O"),
                ExcursionKeyTtl);
        }
        else
        {
            excursionStartUtc = DateTime.Parse(existing.ToString(), null,
                System.Globalization.DateTimeStyles.RoundtripKind);
        }

        return (int)(telemetry.Timestamp - excursionStartUtc).TotalSeconds;
    }

    private class TelemetryWindowPoint
    {
        public double CargoTemperature { get; set; }
        public double FuelLevel { get; set; }
        public double Speed { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
