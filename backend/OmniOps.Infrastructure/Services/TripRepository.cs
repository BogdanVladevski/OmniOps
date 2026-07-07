using Microsoft.EntityFrameworkCore;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Data;

namespace OmniOps.Infrastructure.Services;

public class TripRepository : ITripRepository
{
    private readonly AppDbContext _context;

    public TripRepository(AppDbContext context) => _context = context;

    public Task<Trip?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Trips.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<IReadOnlyList<Trip>> GetByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken = default) =>
        _context.Trips.AsNoTracking().Where(t => t.VehicleId == vehicleId)
            .OrderByDescending(t => t.StartedAtUtc).ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<Trip>)t.Result, cancellationToken);

    public async Task AddAsync(Trip trip, CancellationToken cancellationToken = default) =>
        await _context.Trips.AddAsync(trip, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
