using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;
using StackExchange.Redis;

namespace OmniOps.Infrastructure.Services
{
    public class RedisAnomalyDetectionService : IAnomalyDetectionService
    {
        private readonly IConnectionMultiplexer _redisConnection;
        private readonly ILogger<RedisAnomalyDetectionService> _logger;
        private const int WindowSize = 10;

        public RedisAnomalyDetectionService(IConnectionMultiplexer redisConnection, ILogger<RedisAnomalyDetectionService> logger)
        {
            _redisConnection = redisConnection;
            _logger = logger;
        }

        public async Task<bool> AnalyzeTelemetryAsync(VehicleTelemetry telemetry, CancellationToken cancellationToken = default)
        {
            var db = _redisConnection.GetDatabase();
            var historyKey = $"vehicle:history:{telemetry.VehicleId}";

            try
            {
                // Fetch the previous telemetry point for comparison before pushing the new one
                var previousPayload = await db.ListGetByIndexAsync(historyKey, 0); // index 0 is the newest in standard list prepending
                
                // Push the new point to the head of the list
                var currentPoint = new TelemetryWindowPoint
                {
                    Speed = telemetry.Speed,
                    EngineTemperature = telemetry.EngineTemperature,
                    Timestamp = telemetry.Timestamp
                };
                
                var jsonPoint = JsonSerializer.Serialize(currentPoint);
                await db.ListLeftPushAsync(historyKey, jsonPoint);

                // Trim list to maintain the sliding window size
                await db.ListTrimAsync(historyKey, 0, WindowSize - 1);

                if (previousPayload.HasValue)
                {
                    var previousPoint = JsonSerializer.Deserialize<TelemetryWindowPoint>(previousPayload.ToString());
                    if (previousPoint != null)
                    {
                        // Multi-metric anomaly check:
                        // 1. Sudden speed drop: speed decreased by 20 km/h or more compared to the previous reading.
                        // 2. High engine temperature: engine temperature exceeds 100°C.
                        var speedDrop = previousPoint.Speed - telemetry.Speed;
                        var isEngineHot = telemetry.EngineTemperature > 100.0;

                        if (speedDrop >= 20.0 && isEngineHot)
                        {
                            _logger.LogWarning("⚠️ CRITICAL ANOMALY DETECTED for vehicle {VehicleId}! Engine Temperature: {Temp}°C (High), Speed Drop: {Drop} km/h (Sudden Deceleration).",
                                telemetry.VehicleId, telemetry.EngineTemperature, speedDrop);
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during real-time anomaly detection for vehicle {VehicleId}.", telemetry.VehicleId);
            }

            return false;
        }

        private class TelemetryWindowPoint
        {
            public double Speed { get; set; }
            public double EngineTemperature { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }
}
