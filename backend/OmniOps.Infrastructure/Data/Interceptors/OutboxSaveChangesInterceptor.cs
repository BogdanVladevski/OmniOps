using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;

namespace OmniOps.Infrastructure.Data.Interceptors;

public class OutboxSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICorrelationContext _correlationContext;

    public OutboxSaveChangesInterceptor(ICorrelationContext correlationContext) =>
        _correlationContext = correlationContext;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            CaptureDomainEvents(eventData.Context);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            CaptureDomainEvents(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    private void CaptureDomainEvents(DbContext context)
    {
        var correlationId = _correlationContext.CorrelationId;
        var captured = context.ChangeTracker.Entries<IHasDomainEvents>()
            .Select(entry => entry.Entity)
            .SelectMany(entity =>
            {
                var events = entity.DomainEvents.ToList();
                entity.ClearDomainEvents();
                return events;
            })
            .Select(e => DomainEventCapture.Map(e, correlationId))
            .ToList();

        if (captured.Count > 0)
        {
            context.Set<OutboxMessage>().AddRange(captured.Select(c => c.Outbox));
            context.Set<StoredEvent>().AddRange(captured.Select(c => c.Stored));
        }
    }
}
