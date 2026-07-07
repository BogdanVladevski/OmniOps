using Microsoft.EntityFrameworkCore;
using OmniOps.Core.Entities;
using OmniOps.Core.Enums;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Data;

namespace OmniOps.Infrastructure.Services;

public class IncidentRepository : IIncidentRepository
{
    private readonly AppDbContext _context;

    public IncidentRepository(AppDbContext context) => _context = context;

    public Task<Incident?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Incidents.Include(i => i.Notes).FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Incident>> GetByFleetAsync(
        Guid fleetId, IncidentStatus? status = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Incidents.AsNoTracking().Where(i => i.FleetId == fleetId);
        if (status.HasValue) query = query.Where(i => i.Status == status);
        return await query.OrderByDescending(i => i.DetectedAtUtc).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Incident incident, CancellationToken cancellationToken = default) =>
        await _context.Incidents.AddAsync(incident, cancellationToken);

    public async Task AddNoteAsync(IncidentNote note, CancellationToken cancellationToken = default) =>
        await _context.IncidentNotes.AddAsync(note, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
