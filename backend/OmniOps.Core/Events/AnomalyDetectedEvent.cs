using OmniOps.Core.Enums;
using OmniOps.Core.Interfaces;

namespace OmniOps.Core.Events;

/// <summary>
/// Raised when the anomaly service classifies a telemetry packet as Warning or Critical.
/// Downstream consumers (outbox publisher, SignalR broadcast, future RAG narrative) pull
/// everything they need from here without re-querying.
/// </summary>
public class AnomalyDetectedEvent : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;

    public string VehicleId { get; }
    public AnomalySeverity Severity { get; }
    public int ExcursionDurationSeconds { get; }

    // Populated when an active shipment was resolved for this vehicle.
    public string? ProductName { get; }
    public string? BatchNumber { get; }
    public decimal? ValueAtRiskUsd { get; }

    public string IncidentSummary { get; }

    public AnomalyDetectedEvent(
        string vehicleId,
        AnomalySeverity severity,
        int excursionDurationSeconds,
        string incidentSummary,
        string? productName = null,
        string? batchNumber = null,
        decimal? valueAtRiskUsd = null)
    {
        VehicleId = vehicleId;
        Severity = severity;
        ExcursionDurationSeconds = excursionDurationSeconds;
        IncidentSummary = incidentSummary;
        ProductName = productName;
        BatchNumber = batchNumber;
        ValueAtRiskUsd = valueAtRiskUsd;
    }
}
