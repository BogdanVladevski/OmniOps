using Microsoft.EntityFrameworkCore;
using OmniOps.Core.Entities;
using OmniOps.Core.Enums;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Data;

namespace OmniOps.Infrastructure.Services;

public class FleetRepository : IFleetRepository
{
    private readonly AppDbContext _context;

    public FleetRepository(AppDbContext context) => _context = context;

    public Task<Fleet?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Fleets.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public Task<IReadOnlyList<Fleet>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _context.Fleets.AsNoTracking().OrderBy(f => f.Name).ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<Fleet>)t.Result, cancellationToken);

    public async Task AddAsync(Fleet fleet, CancellationToken cancellationToken = default) =>
        await _context.Fleets.AddAsync(fleet, cancellationToken);

    public Task<int> CountVehiclesAsync(Guid fleetId, CancellationToken cancellationToken = default) =>
        _context.Vehicles.CountAsync(v => v.FleetId == fleetId, cancellationToken);

    public Task<int> CountActiveTripsAsync(Guid fleetId, CancellationToken cancellationToken = default) =>
        _context.Trips.CountAsync(
            t => t.Vehicle.FleetId == fleetId && t.Status == TripStatus.InProgress,
            cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
