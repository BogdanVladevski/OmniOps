namespace OmniOps.Core.Domain;

/// <summary>Result returned by IAnomalyDetectionService after evaluating a telemetry packet.</summary>
/// <param name="IsAnomaly">Whether an anomaly condition was detected.</param>
/// <param name="ExcursionDurationSeconds">
/// How many consecutive seconds cargo temperature has been outside the shipment's safe range.
/// Zero when no active shipment or temperature is within range.
/// </param>
public record AnomalyAnalysis(bool IsAnomaly, int ExcursionDurationSeconds = 0);
