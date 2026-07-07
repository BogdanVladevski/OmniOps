using OmniOps.Core.Entities;

namespace OmniOps.Core.Interfaces;

public interface IDepotRepository
{
    Task<IReadOnlyList<Depot>> GetByFleetIdAsync(Guid fleetId, CancellationToken cancellationToken = default);
    Task AddAsync(Depot depot, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
