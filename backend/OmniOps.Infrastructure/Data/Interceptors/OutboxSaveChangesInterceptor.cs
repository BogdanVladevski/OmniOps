using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;

namespace OmniOps.Infrastructure.Data.Interceptors;

public class OutboxSaveChangesInterceptor : SaveChangesInterceptor
{
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

    private static void CaptureDomainEvents(DbContext context)
    {
        var outboxMessages = context.ChangeTracker.Entries<IHasDomainEvents>()
            .Select(entry => entry.Entity)
            .SelectMany(entity =>
            {
                var events = entity.DomainEvents.ToList();
                entity.ClearDomainEvents();
                return events;
            })
            .Select(domainEvent => new OutboxMessage
            {
                Id = Guid.NewGuid(),
                OccurredOnUtc = DateTime.UtcNow,
                Type = domainEvent.GetType().Name,
                Content = JsonSerializer.Serialize(domainEvent, domainEvent.GetType())
            })
            .ToList();

        if (outboxMessages.Count > 0)
        {
            context.Set<OutboxMessage>().AddRange(outboxMessages);
        }
    }
}
