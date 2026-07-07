namespace OmniOps.Core.Interfaces;

public interface IPredictionService
{
    Task<VehicleHealthPrediction> PredictVehicleHealthAsync(string vehicleExternalId, CancellationToken cancellationToken = default);
    Task<MaintenancePrediction> PredictMaintenanceAsync(string vehicleExternalId, CancellationToken cancellationToken = default);
    Task<DriverRiskPrediction> PredictDriverRiskAsync(Guid driverId, CancellationToken cancellationToken = default);
    Task<FuelPrediction> PredictFuelAsync(string vehicleExternalId, CancellationToken cancellationToken = default);
    Task<BatteryPrediction> PredictBatteryAsync(string vehicleExternalId, CancellationToken cancellationToken = default);
    Task<EtaPrediction> PredictEtaAsync(Guid tripId, CancellationToken cancellationToken = default);
}

public record VehicleHealthPrediction(string VehicleId, double Score, string Summary);
public record MaintenancePrediction(string VehicleId, int DaysUntilService, string Recommendation);
public record DriverRiskPrediction(Guid DriverId, double RiskScore, string Summary);
public record FuelPrediction(string VehicleId, double EstimatedRangeKm, string Summary);
public record BatteryPrediction(string VehicleId, double DegradationPercent, string Summary);
public record EtaPrediction(Guid TripId, DateTime? EstimatedArrivalUtc, string Summary);
