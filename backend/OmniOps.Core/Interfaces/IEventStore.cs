using OmniOps.Core.Entities;

namespace OmniOps.Core.Interfaces;

public interface IEventStore
{
    Task AppendAsync(StoredEvent storedEvent, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StoredEvent>> GetByTimeRangeAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string? eventType = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StoredEvent>> GetByAggregateAsync(
        string aggregateType,
        string aggregateId,
        CancellationToken cancellationToken = default);
}
