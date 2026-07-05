using System.Text.Json;
using Microsoft.Extensions.Logging;
using OmniOps.Core.Domain;
using OmniOps.Core.Entities;
using OmniOps.Core.Enums;
using OmniOps.Core.Interfaces;
using StackExchange.Redis;

namespace OmniOps.Infrastructure.Services;

public class RedisAnomalyDetectionService : IAnomalyDetectionService
{
    private readonly IConnectionMultiplexer _redisConnection;
    private readonly ILogger<RedisAnomalyDetectionService> _logger;

    private const int RollingWindowSize = 20;
    private static readonly TimeSpan KeyTtl = TimeSpan.FromHours(24);

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
            var point = new TempReading
            {
                CargoTemperature = telemetry.EngineTemperature,
                Timestamp = telemetry.Timestamp
            };

            await db.ListLeftPushAsync(historyKey, JsonSerializer.Serialize(point));
            await db.ListTrimAsync(historyKey, 0, RollingWindowSize - 1);
            await db.KeyExpireAsync(historyKey, KeyTtl);

            var raw = await db.ListRangeAsync(historyKey, 0, RollingWindowSize - 1);
            var temps = raw
                .Select(r => JsonSerializer.Deserialize<TempReading>(r.ToString()))
                .Where(r => r is not null)
                .Cast<TempReading>()
                .OrderBy(r => r.Timestamp)
                .Select(r => r.CargoTemperature)
                .ToList();

            var excursionSeconds = await UpdateExcursionDurationAsync(
                db, excursionKey, telemetry, shipment);

            // Hard compliance: temp is outside safe range right now — always flag.
            if (shipment is not null)
            {
                var temp = telemetry.EngineTemperature;
                var outsideRange = temp > shipment.MaxSafeTempCelsius || temp < shipment.MinSafeTempCelsius;

                if (outsideRange)
                {
                    var severity = AnomalyClassifier.ClassifyExcursion(excursionSeconds);

                    _logger.LogWarning(
                        "{Severity} excursion: {Product} batch {Batch} on {VehicleId} at {Temp}°C " +
                        "(safe {Min}–{Max}°C) for {Duration}s",
                        severity, shipment.ProductName, shipment.BatchNumber,
                        telemetry.VehicleId, temp,
                        shipment.MinSafeTempCelsius, shipment.MaxSafeTempCelsius,
                        excursionSeconds);

                    return new AnomalyAnalysis(true, severity, excursionSeconds);
                }
            }

            // Soft early-warning: still in range but heading toward it fast.
            if (shipment is not null && AnomalyClassifier.IsTrendingTowardBreach(
                    temps, shipment.MinSafeTempCelsius, shipment.MaxSafeTempCelsius))
            {
                _logger.LogInformation(
                    "Trend warning for {VehicleId}: {Product} cargo temp at {Temp}°C moving toward safe-range boundary",
                    telemetry.VehicleId, shipment.ProductName, telemetry.EngineTemperature);

                return new AnomalyAnalysis(true, AnomalySeverity.Warning, 0);
            }

            return new AnomalyAnalysis(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Anomaly analysis failed for {VehicleId}", telemetry.VehicleId);
        }

        return new AnomalyAnalysis(false);
    }

    // Stores excursion start timestamp in Redis; resets when temp comes back in range.
    private static async Task<int> UpdateExcursionDurationAsync(
        IDatabase db,
        string excursionKey,
        VehicleTelemetry telemetry,
        Shipment? shipment)
    {
        if (shipment is null)
            return 0;

        var temp = telemetry.EngineTemperature;
        var inRange = temp >= shipment.MinSafeTempCelsius && temp <= shipment.MaxSafeTempCelsius;

        if (inRange)
        {
            await db.KeyDeleteAsync(excursionKey);
            return 0;
        }

        var existing = await db.StringGetAsync(excursionKey);
        DateTime startUtc;

        if (existing.IsNullOrEmpty)
        {
            startUtc = telemetry.Timestamp;
            await db.StringSetAsync(excursionKey, startUtc.ToString("O"), KeyTtl);
        }
        else
        {
            startUtc = DateTime.Parse(
                existing.ToString(), null,
                System.Globalization.DateTimeStyles.RoundtripKind);
        }

        return (int)(telemetry.Timestamp - startUtc).TotalSeconds;
    }

    private class TempReading
    {
        public double CargoTemperature { get; set; }
        public DateTime Timestamp { get; set; }
    }
}

