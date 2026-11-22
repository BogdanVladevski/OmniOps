using Microsoft.EntityFrameworkCore;
using OmniOps.Core.Entities;
using OmniOps.Core.Enums;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Data;

namespace OmniOps.Infrastructure.Services;

public class JobRepository : IJobRepository
{
    private readonly AppDbContext _context;

    public JobRepository(AppDbContext context) => _context = context;

    public Task<Trip?> GetAsync(Guid jobId, Guid organizationId, CancellationToken cancellationToken = default) =>
        _context.Trips
            .Include(t => t.Vehicle)
            .Include(t => t.Driver)
            .Include(t => t.Documents)
            .FirstOrDefaultAsync(t => t.Id == jobId && t.OrganizationId == organizationId, cancellationToken);

    public async Task<IReadOnlyList<Trip>> ListAsync(
        Guid organizationId,
        IReadOnlyCollection<TripStatus> statuses,
        Guid? driverId,
        DateTime? completedSinceUtc,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Trips
            .AsNoTracking()
            .Include(t => t.Vehicle)
            .Include(t => t.Driver)
            .Include(t => t.Documents)
            .Where(t => t.OrganizationId == organizationId && statuses.Contains(t.Status));

        if (driverId is { } d)
        {
            query = query.Where(t => t.DriverId == d);
        }

        if (completedSinceUtc is { } since)
        {
            query = query.Where(t => t.Status != TripStatus.Completed || t.CompletedAtUtc >= since);
        }

        return await query
            .OrderByDescending(t => t.Status == TripStatus.InProgress)
            .ThenBy(t => t.Status == TripStatus.Completed)
            .ThenByDescending(t => t.CompletedAtUtc ?? t.CreatedAtUtc)
            .Take(take)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    public Task<Trip?> GetByTrackingTokenAsync(string trackingToken, CancellationToken cancellationToken = default) =>
        _context.Trips.AsNoTracking().FirstOrDefaultAsync(t => t.TrackingToken == trackingToken, cancellationToken);

    public Task<Vehicle?> GetVehicleAsync(Guid vehicleId, Guid organizationId, CancellationToken cancellationToken = default) =>
        _context.Vehicles
            .Include(v => v.Fleet)
            .FirstOrDefaultAsync(v => v.Id == vehicleId && v.Fleet.OrganizationId == organizationId, cancellationToken);

    public Task<Driver?> GetDriverAsync(Guid driverId, Guid organizationId, CancellationToken cancellationToken = default) =>
        _context.Drivers
            .Include(d => d.Fleet)
            .FirstOrDefaultAsync(d => d.Id == driverId && d.Fleet.OrganizationId == organizationId, cancellationToken);

    public async Task<IReadOnlyList<Vehicle>> ListVehiclesAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        await _context.Vehicles.AsNoTracking()
            .Where(v => v.Fleet.OrganizationId == organizationId && v.Status != VehicleOperationalStatus.Retired)
            .OrderBy(v => v.ExternalId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Driver>> ListDriversAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        await _context.Drivers.AsNoTracking()
            .Where(d => d.Fleet.OrganizationId == organizationId)
            .OrderBy(d => d.FullName)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Trip job, CancellationToken cancellationToken = default) =>
        await _context.Trips.AddAsync(job, cancellationToken);

    public async Task AddDocumentAsync(TripDocument document, CancellationToken cancellationToken = default) =>
        await _context.TripDocuments.AddAsync(document, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
