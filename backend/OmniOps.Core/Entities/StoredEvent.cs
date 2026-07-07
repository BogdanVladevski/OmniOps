namespace OmniOps.Core.Entities;

/// <summary>
/// Append-only event store record. Captures every domain event with schema version for replay and auditing.
/// </summary>
public class StoredEvent
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int SchemaVersion { get; set; }
    public string AggregateType { get; set; } = string.Empty;
    public string AggregateId { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime OccurredOnUtc { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime? ReplayedAtUtc { get; set; }
}
