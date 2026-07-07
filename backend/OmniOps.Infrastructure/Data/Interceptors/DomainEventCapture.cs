using System.Text.Json;
using OmniOps.Core.Entities;
using OmniOps.Core.Events;
using OmniOps.Core.Interfaces;

namespace OmniOps.Infrastructure.Data.Interceptors;

internal static class DomainEventCapture
{
    public static (OutboxMessage Outbox, StoredEvent Stored) Map(IDomainEvent domainEvent, string? correlationId = null)
    {
        var eventType = domainEvent.GetType().Name;
        var (aggregateType, aggregateId) = ResolveAggregate(domainEvent);
        var payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType());
        var occurredOn = domainEvent.OccurredOn;

        var outbox = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            OccurredOnUtc = occurredOn,
            Type = eventType,
            Content = payload
        };

        var stored = new StoredEvent
        {
            Id = Guid.NewGuid(),
            EventType = eventType,
            SchemaVersion = EventSchemaVersions.Resolve(eventType),
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            Payload = payload,
            OccurredOnUtc = occurredOn,
            CorrelationId = correlationId
        };

        return (outbox, stored);
    }

    private static (string AggregateType, string AggregateId) ResolveAggregate(IDomainEvent domainEvent) =>
        domainEvent switch
        {
            TelemetryReceivedEvent e => ("Vehicle", e.Telemetry.VehicleId),
            AnomalyDetectedEvent e => ("Vehicle", e.VehicleId),
            VehicleAssignedEvent e => ("Vehicle", e.VehicleId.ToString()),
            TripStartedEvent e => ("Trip", e.TripId.ToString()),
            TripCompletedEvent e => ("Trip", e.TripId.ToString()),
            GeofenceBreachedEvent e => ("Vehicle", e.VehicleId),
            _ => ("Unknown", Guid.NewGuid().ToString())
        };
}
