using OmniOps.Core.Entities;

namespace OmniOps.Core.Interfaces;

public interface IGeofenceRepository
{
    Task<Geofence?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Geofence>> GetActiveAsync(Guid? fleetId = null, CancellationToken cancellationToken = default);
    Task AddAsync(Geofence geofence, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
