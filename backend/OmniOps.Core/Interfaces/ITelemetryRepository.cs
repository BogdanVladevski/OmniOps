using OmniOps.Core.Entities;

namespace OmniOps.Core.Interfaces;

public interface ITelemetryRepository
{
    Task AddAsync(VehicleTelemetry telemetry, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
