using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using OmniOps.Core.Domain;
using OmniOps.Core.Entities;
using OmniOps.Core.Enums;
using OmniOps.Core.Interfaces;

namespace OmniOps.Infrastructure.Services;

/// <summary>
/// Cargo-temperature excursion and trend detection. Keeps a short rolling window and the excursion
/// start per (fleet, vehicle) in process memory — correct for a single API instance; a restart resets
/// excursion timers (the next out-of-range reading still alerts). Must be registered as a singleton.
/// </summary>
public class InMemoryAnomalyDetectionService : IAnomalyDetectionService
{
    private readonly ConcurrentDictionary<string, VehicleState> _states = new();
    private readonly ILogger<InMemoryAnomalyDetectionService> _logger;

    private const int RollingWindowSize = 20;
    private static readonly TimeSpan StateTtl = TimeSpan.FromHours(24);

    public InMemoryAnomalyDetectionService(ILogger<InMemoryAnomalyDetectionService> logger)
    {
        _logger = logger;
    }

    public Task<AnomalyAnalysis> AnalyzeTelemetryAsync(
        VehicleTelemetry telemetry,
        Shipment? shipment = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Task.FromResult(Analyze(telemetry, shipment));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Anomaly analysis failed for {VehicleId}", telemetry.VehicleId);
            return Task.FromResult(new AnomalyAnalysis(false));
        }
    }

    private AnomalyAnalysis Analyze(VehicleTelemetry telemetry, Shipment? shipment)
    {
        // ExternalId is unique per fleet only, so state is keyed by fleet as well as vehicle.
        var key = $"{shipment?.FleetId ?? Guid.Empty}:{telemetry.VehicleId}";
        var state = _states.GetOrAdd(key, _ => new VehicleState());

        List<double> temps;
        int excursionSeconds;
        lock (state)
        {
            if (telemetry.Timestamp - state.LastSeenUtc > StateTtl)
            {
                state.History.Clear();
                state.ExcursionStartUtc = null;
            }

            state.LastSeenUtc = telemetry.Timestamp;
            state.History.Add((telemetry.Timestamp, telemetry.EngineTemperature));
            if (state.History.Count > RollingWindowSize)
            {
                state.History.RemoveRange(0, state.History.Count - RollingWindowSize);
            }

            temps = state.History.OrderBy(r => r.Timestamp).Select(r => r.CargoTemperature).ToList();
            excursionSeconds = UpdateExcursionDuration(state, telemetry, shipment);
        }

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

    // Records when the current excursion started; resets when temp comes back in range.
    private static int UpdateExcursionDuration(VehicleState state, VehicleTelemetry telemetry, Shipment? shipment)
    {
        if (shipment is null)
            return 0;

        var temp = telemetry.EngineTemperature;
        var inRange = temp >= shipment.MinSafeTempCelsius && temp <= shipment.MaxSafeTempCelsius;

        if (inRange)
        {
            state.ExcursionStartUtc = null;
            return 0;
        }

        state.ExcursionStartUtc ??= telemetry.Timestamp;
        return (int)(telemetry.Timestamp - state.ExcursionStartUtc.Value).TotalSeconds;
    }

    private sealed class VehicleState
    {
        public List<(DateTime Timestamp, double CargoTemperature)> History { get; } = new();
        public DateTime? ExcursionStartUtc { get; set; }
        public DateTime LastSeenUtc { get; set; }
    }
}
