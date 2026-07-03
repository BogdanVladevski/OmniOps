namespace OmniOps.Core.Interfaces;

public interface IDeduplicationService
{
    Task<bool> TryAcquireProcessingLockAsync(Guid packetId, CancellationToken cancellationToken = default);

    Task ReleaseProcessingLockAsync(Guid packetId, CancellationToken cancellationToken = default);
}
