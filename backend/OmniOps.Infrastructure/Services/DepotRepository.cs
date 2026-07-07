using Microsoft.EntityFrameworkCore;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Data;

namespace OmniOps.Infrastructure.Services;

public class DepotRepository : IDepotRepository
{
    private readonly AppDbContext _context;

    public DepotRepository(AppDbContext context) => _context = context;

    public Task<IReadOnlyList<Depot>> GetByFleetIdAsync(Guid fleetId, CancellationToken cancellationToken = default) =>
        _context.Depots.AsNoTracking().Where(d => d.FleetId == fleetId).ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<Depot>)t.Result, cancellationToken);

    public async Task AddAsync(Depot depot, CancellationToken cancellationToken = default) =>
        await _context.Depots.AddAsync(depot, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
