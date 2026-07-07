using Microsoft.EntityFrameworkCore;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Data;

namespace OmniOps.Infrastructure.Services;

public class VehicleRepository : IVehicleRepository
{
    private readonly AppDbContext _context;

    public VehicleRepository(AppDbContext context) => _context = context;

    public Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Vehicles.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

    public Task<Vehicle?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default) =>
        _context.Vehicles.FirstOrDefaultAsync(v => v.ExternalId == externalId, cancellationToken);

    public Task<IReadOnlyList<Vehicle>> GetByFleetIdAsync(Guid fleetId, CancellationToken cancellationToken = default) =>
        _context.Vehicles.AsNoTracking().Where(v => v.FleetId == fleetId).ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<Vehicle>)t.Result, cancellationToken);

    public async Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken = default) =>
        await _context.Vehicles.AddAsync(vehicle, cancellationToken);

    public async Task AddMaintenanceRecordAsync(VehicleMaintenanceRecord record, CancellationToken cancellationToken = default) =>
        await _context.VehicleMaintenanceRecords.AddAsync(record, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
