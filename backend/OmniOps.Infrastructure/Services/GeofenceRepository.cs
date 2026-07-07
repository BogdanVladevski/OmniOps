using Microsoft.EntityFrameworkCore;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Data;

namespace OmniOps.Infrastructure.Services;

public class GeofenceRepository : IGeofenceRepository
{
    private readonly AppDbContext _context;

    public GeofenceRepository(AppDbContext context) => _context = context;

    public Task<Geofence?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Geofences.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

    public Task<IReadOnlyList<Geofence>> GetActiveAsync(Guid? fleetId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Geofences.AsNoTracking().Where(g => g.IsActive);
        if (fleetId.HasValue)
        {
            query = query.Where(g => g.FleetId == null || g.FleetId == fleetId);
        }

        return query.ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<Geofence>)t.Result, cancellationToken);
    }

    public async Task AddAsync(Geofence geofence, CancellationToken cancellationToken = default) =>
        await _context.Geofences.AddAsync(geofence, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
