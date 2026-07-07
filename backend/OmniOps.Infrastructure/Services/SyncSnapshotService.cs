using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Data;

namespace OmniOps.Infrastructure.Services;

public class SyncSnapshotService : ISyncSnapshotService
{
    private readonly AppDbContext _context;
    private readonly INotificationRepository _notifications;

    public SyncSnapshotService(AppDbContext context, INotificationRepository notifications)
    {
        _context = context;
        _notifications = notifications;
    }

    public async Task<SyncSnapshot> GetSnapshotAsync(
        Guid organizationId,
        string userId,
        DateTime? sinceUtc,
        CancellationToken cancellationToken = default)
    {
        var since = sinceUtc ?? DateTime.UtcNow.AddHours(-24);
        var vehicles = await _context.Vehicles.AsNoTracking()
            .Where(v => v.Fleet!.OrganizationId == organizationId)
            .Select(v => new SyncVehicle(v.ExternalId, v.Status.ToString(), v.Registration))
            .ToListAsync(cancellationToken);

        var notifications = await _notifications.GetForUserAsync(userId, organizationId, 50, cancellationToken);
        var recent = notifications.Where(n => n.CreatedAtUtc >= since).ToList();

        return new SyncSnapshot(DateTime.UtcNow, vehicles, recent);
    }
}
