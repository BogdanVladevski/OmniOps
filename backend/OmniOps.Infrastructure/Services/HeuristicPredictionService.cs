using Microsoft.EntityFrameworkCore;
using OmniOps.Core.Enums;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Data;

namespace OmniOps.Infrastructure.Services;

/// <summary>Heuristic, ML-ready prediction service. Swap implementations without changing API contracts.</summary>
public class HeuristicPredictionService : IPredictionService
{
    private readonly AppDbContext _context;
    private readonly ITelemetryCacheService _cache;

    public HeuristicPredictionService(AppDbContext context, ITelemetryCacheService cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<VehicleHealthPrediction> PredictVehicleHealthAsync(
        string vehicleExternalId, CancellationToken cancellationToken = default)
    {
        var telemetry = await _cache.GetLatestTelemetryAsync(vehicleExternalId);
        var maintenanceCount = await _context.VehicleMaintenanceRecords
            .CountAsync(m => m.Vehicle.ExternalId == vehicleExternalId, cancellationToken);

        var score = 100.0;
        if (telemetry?.FuelLevel < 25) score -= 15;
        if (telemetry?.BatteryLevel < 30) score -= 20;
        if (telemetry?.EngineTemperature > 100) score -= 10;
        score -= maintenanceCount * 2;
        score = Math.Clamp(score, 0, 100);

        return new VehicleHealthPrediction(vehicleExternalId, score,
            score >= 80 ? "Vehicle operating within normal parameters." : "Elevated wear indicators — schedule inspection.");
    }

    public async Task<MaintenancePrediction> PredictMaintenanceAsync(
        string vehicleExternalId, CancellationToken cancellationToken = default)
    {
        var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.ExternalId == vehicleExternalId, cancellationToken);
        var lastService = vehicle is null
            ? null
            : await _context.VehicleMaintenanceRecords
                .Where(m => m.VehicleId == vehicle.Id)
                .OrderByDescending(m => m.PerformedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

        var daysSince = lastService is null ? 90 : (DateTime.UtcNow - lastService.PerformedAtUtc).Days;
        var daysUntil = Math.Max(0, 90 - daysSince);

        return new MaintenancePrediction(vehicleExternalId, daysUntil,
            daysUntil < 14 ? "Service due soon based on maintenance interval." : "No immediate maintenance required.");
    }

    public async Task<DriverRiskPrediction> PredictDriverRiskAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        var driver = await _context.Drivers.AsNoTracking().FirstOrDefaultAsync(d => d.Id == driverId, cancellationToken)
            ?? throw new KeyNotFoundException($"Driver {driverId} not found.");
        var risk = Math.Clamp(100 - driver.SafetyScore, 0, 100);
        return new DriverRiskPrediction(driverId, risk,
            risk > 30 ? "Elevated risk profile — review recent incidents." : "Driver within acceptable risk band.");
    }

    public async Task<FuelPrediction> PredictFuelAsync(
        string vehicleExternalId, CancellationToken cancellationToken = default)
    {
        var telemetry = await _cache.GetLatestTelemetryAsync(vehicleExternalId);
        var fuel = telemetry?.FuelLevel ?? 50;
        var range = fuel * 8;
        return new FuelPrediction(vehicleExternalId, range,
            $"Estimated {range:F0} km range at current fuel level.");
    }

    public async Task<BatteryPrediction> PredictBatteryAsync(
        string vehicleExternalId, CancellationToken cancellationToken = default)
    {
        var telemetry = await _cache.GetLatestTelemetryAsync(vehicleExternalId);
        var level = telemetry?.BatteryLevel ?? 80;
        var degradation = Math.Clamp(100 - level, 0, 100);
        return new BatteryPrediction(vehicleExternalId, degradation,
            degradation > 40 ? "Battery degradation trending high." : "Battery within expected range.");
    }

    public async Task<EtaPrediction> PredictEtaAsync(Guid tripId, CancellationToken cancellationToken = default)
    {
        var trip = await _context.Trips.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tripId, cancellationToken)
            ?? throw new KeyNotFoundException($"Trip {tripId} not found.");
        var eta = trip.StartedAtUtc?.AddHours(4) ?? DateTime.UtcNow.AddHours(2);
        return new EtaPrediction(tripId, eta, $"Estimated arrival based on trip start and fleet averages.");
    }
}
