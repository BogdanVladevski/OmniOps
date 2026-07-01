using OmniOps.Core.Entities;

namespace OmniOps.Core.Interfaces;

public interface ITelemetryBroadcastService
{
    Task BroadcastTelemetryUpdateAsync(VehicleTelemetry telemetry, CancellationToken cancellationToken = default);
    Task BroadcastPlaybookInstructionsAsync(string vehicleId, string instructions, CancellationToken cancellationToken = default);
}
