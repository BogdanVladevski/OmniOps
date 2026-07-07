using OmniOps.Core.Entities;

namespace OmniOps.Core.Interfaces;

public interface IIncidentDetectionService
{
    Task<IReadOnlyList<Incident>> DetectAsync(
        VehicleTelemetry telemetry,
        CancellationToken cancellationToken = default);
}
