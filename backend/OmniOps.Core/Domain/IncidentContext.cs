using OmniOps.Core.Enums;

namespace OmniOps.Core.Domain;

/// <summary>
/// The concrete incident facts used to both retrieve relevant playbooks and prompt the
/// narrative generator. Assembled from the anomaly analysis + active shipment.
/// </summary>
public class IncidentContext
{
    public string VehicleId { get; init; } = string.Empty;
    public AnomalySeverity Severity { get; init; }
    public int ExcursionDurationSeconds { get; init; }
    public double TemperatureCelsius { get; init; }

    public string? ProductName { get; init; }
    public string? BatchNumber { get; init; }
    public decimal? ValueAtRiskUsd { get; init; }
    public double? MinSafeTempCelsius { get; init; }
    public double? MaxSafeTempCelsius { get; init; }

    /// <summary>The deterministic summary already computed by the telemetry handler.</summary>
    public string IncidentSummary { get; init; } = string.Empty;
}
