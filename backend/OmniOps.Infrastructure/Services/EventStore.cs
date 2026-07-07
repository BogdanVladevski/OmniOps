using Microsoft.EntityFrameworkCore;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Data;

namespace OmniOps.Infrastructure.Services;

public class EventStore : IEventStore
{
    private readonly AppDbContext _context;

    public EventStore(AppDbContext context)
    {
        _context = context;
    }

    public async Task AppendAsync(StoredEvent storedEvent, CancellationToken cancellationToken = default)
    {
        await _context.StoredEvents.AddAsync(storedEvent, cancellationToken);
    }

    public async Task<IReadOnlyList<StoredEvent>> GetByTimeRangeAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string? eventType = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.StoredEvents
            .AsNoTracking()
            .Where(e => e.OccurredOnUtc >= fromUtc && e.OccurredOnUtc <= toUtc);

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            query = query.Where(e => e.EventType == eventType);
        }

        return await query.OrderBy(e => e.OccurredOnUtc).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StoredEvent>> GetByAggregateAsync(
        string aggregateType,
        string aggregateId,
        CancellationToken cancellationToken = default)
    {
        return await _context.StoredEvents
            .AsNoTracking()
            .Where(e => e.AggregateType == aggregateType && e.AggregateId == aggregateId)
            .OrderBy(e => e.OccurredOnUtc)
            .ToListAsync(cancellationToken);
    }
}
