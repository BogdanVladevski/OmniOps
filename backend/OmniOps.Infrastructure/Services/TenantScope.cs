using Microsoft.EntityFrameworkCore;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Data;

namespace OmniOps.Infrastructure.Services;

public class TenantScope : ITenantScope
{
    private readonly AppDbContext _context;
    private readonly ITenantContext _tenant;

    public TenantScope(AppDbContext context, ITenantContext tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    private Guid Org => _tenant.OrganizationId;

    public async Task EnsureFleetAsync(Guid fleetId, CancellationToken cancellationToken = default)
    {
        if (!await _context.Fleets.AnyAsync(f => f.Id == fleetId && f.OrganizationId == Org, cancellationToken))
            throw new KeyNotFoundException($"Fleet {fleetId} not found.");
    }

    public async Task EnsureVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        if (!await _context.Vehicles.AnyAsync(v => v.Id == vehicleId && v.Fleet.OrganizationId == Org, cancellationToken))
            throw new KeyNotFoundException($"Vehicle {vehicleId} not found.");
    }

    public async Task EnsureDriverAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        if (!await _context.Drivers.AnyAsync(d => d.Id == driverId && d.Fleet.OrganizationId == Org, cancellationToken))
            throw new KeyNotFoundException($"Driver {driverId} not found.");
    }

    public async Task EnsureIncidentAsync(Guid incidentId, CancellationToken cancellationToken = default)
    {
        if (!await _context.Incidents.AnyAsync(i => i.Id == incidentId && i.OrganizationId == Org, cancellationToken))
            throw new KeyNotFoundException($"Incident {incidentId} not found.");
    }

    public async Task<Guid> FleetOfVehicleAsync(string externalId, CancellationToken cancellationToken = default)
    {
        var fleets = await _context.Vehicles.AsNoTracking()
            .Where(v => v.ExternalId == externalId && v.Fleet.OrganizationId == Org)
            .Select(v => v.FleetId).Distinct().Take(2).ToListAsync(cancellationToken);
        return fleets.Count == 1 ? fleets[0] : throw new KeyNotFoundException($"Vehicle {externalId} not found.");
    }

    public async Task<IReadOnlyList<Guid>> FleetIdsAsync(CancellationToken cancellationToken = default) =>
        await _context.Fleets.AsNoTracking().Where(f => f.OrganizationId == Org).Select(f => f.Id).ToListAsync(cancellationToken);
}
