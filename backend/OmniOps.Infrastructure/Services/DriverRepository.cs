using Microsoft.EntityFrameworkCore;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Data;

namespace OmniOps.Infrastructure.Services;

public class DriverRepository : IDriverRepository
{
    private readonly AppDbContext _context;

    public DriverRepository(AppDbContext context) => _context = context;

    public Task<Driver?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Drivers.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<IReadOnlyList<Driver>> GetByFleetIdAsync(Guid fleetId, CancellationToken cancellationToken = default) =>
        _context.Drivers.AsNoTracking().Where(d => d.FleetId == fleetId).ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<Driver>)t.Result, cancellationToken);

    public async Task AddAsync(Driver driver, CancellationToken cancellationToken = default) =>
        await _context.Drivers.AddAsync(driver, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
