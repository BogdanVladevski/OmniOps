using OmniOps.Core.Entities;
using OmniOps.Core.Events;

namespace OmniOps.Core.Interfaces;

public interface IGeofenceDetectionService
{
    Task<IReadOnlyList<GeofenceBreachedEvent>> DetectBreachesAsync(
        VehicleTelemetry telemetry,
        CancellationToken cancellationToken = default);
}
