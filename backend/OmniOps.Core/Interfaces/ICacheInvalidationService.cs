namespace OmniOps.Core.Interfaces;

public interface ICacheInvalidationService
{
    Task InvalidateVehicleTelemetryAsync(string vehicleId, CancellationToken cancellationToken = default);
    Task InvalidateFleetSnapshotAsync(CancellationToken cancellationToken = default);
}
