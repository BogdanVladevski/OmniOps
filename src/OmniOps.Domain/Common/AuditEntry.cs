namespace OmniOps.Domain.Common;

/// <summary>Who changed what, and when. Each organization only ever sees its own entries.</summary>
public class AuditEntry
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public Guid? UserId { get; set; }
    public string? Details { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}
