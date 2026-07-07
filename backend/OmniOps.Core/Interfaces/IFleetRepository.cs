using OmniOps.Core.Entities;

namespace OmniOps.Core.Interfaces;

public interface IFleetRepository
{
    Task<Fleet?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Fleet>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Fleet fleet, CancellationToken cancellationToken = default);
    Task<int> CountVehiclesAsync(Guid fleetId, CancellationToken cancellationToken = default);
    Task<int> CountActiveTripsAsync(Guid fleetId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
