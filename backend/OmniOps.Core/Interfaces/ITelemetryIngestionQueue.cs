using OmniOps.Core.Entities;

namespace OmniOps.Core.Interfaces;

/// <summary>
/// Hands telemetry to the background ingestion worker, which runs it through
/// <c>ProcessTelemetryCommand</c> with bounded retries. In-process: anything still queued is lost if
/// the API process stops, so callers that need durability must process synchronously instead.
/// </summary>
public interface ITelemetryIngestionQueue
{
    ValueTask EnqueueAsync(VehicleTelemetry telemetry, CancellationToken cancellationToken = default);
}
