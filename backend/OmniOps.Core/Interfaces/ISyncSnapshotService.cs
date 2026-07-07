using OmniOps.Core.Entities;

namespace OmniOps.Core.Interfaces;

public interface ISyncSnapshotService
{
    Task<SyncSnapshot> GetSnapshotAsync(Guid organizationId, string userId, DateTime? sinceUtc, CancellationToken cancellationToken = default);
}

public record SyncSnapshot(DateTime ServerTimeUtc, IReadOnlyList<SyncVehicle> Vehicles, IReadOnlyList<Notification> Notifications);

public record SyncVehicle(string ExternalId, string Status, string? Registration);
