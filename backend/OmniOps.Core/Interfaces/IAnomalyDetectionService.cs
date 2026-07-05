using OmniOps.Core.Domain;
using OmniOps.Core.Entities;

namespace OmniOps.Core.Interfaces;

public interface IAnomalyDetectionService
{
    /// <summary>
    /// Analyzes a telemetry packet for temperature excursions and compound anomalies.
    /// </summary>
    /// <param name="telemetry">The incoming telemetry packet.</param>
    /// <param name="shipment">The active shipment on the vehicle, if any; used for safe-range checks.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<AnomalyAnalysis> AnalyzeTelemetryAsync(
        VehicleTelemetry telemetry,
        Shipment? shipment = null,
        CancellationToken cancellationToken = default);
}
