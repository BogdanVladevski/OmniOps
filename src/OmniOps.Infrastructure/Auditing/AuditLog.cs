using OmniOps.Application.Common.Interfaces;
using OmniOps.Domain.Common;
using OmniOps.Infrastructure.Persistence;

namespace OmniOps.Infrastructure.Auditing;

/// <summary>Adds audit entries to the unit of work: they commit with the change they describe, or not at all.</summary>
internal sealed class AuditLog(AppDbContext db, ITenantContext tenant, TimeProvider clock) : IAuditLog
{
    public void Record(string action, string entityType, string? entityId, string? details = null) =>
        Record(tenant.OrganizationId, tenant.UserId, action, entityType, entityId, details);

    public void Record(Guid organizationId, Guid? userId, string action, string entityType, string? entityId, string? details = null) =>
        db.AuditEntries.Add(new AuditEntry
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details is { Length: > 500 } ? details[..500] : details,
            OccurredAtUtc = clock.GetUtcNow().UtcDateTime,
        });
}
