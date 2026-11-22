using OmniOps.Core.Entities;

namespace OmniOps.Core.Interfaces;

public interface IAuditLogRepository
{
    Task<IReadOnlyList<AuditLog>> QueryAsync(
        Guid organizationId,
        DateTime? fromUtc,
        DateTime? toUtc,
        string? entityType,
        int limit,
        CancellationToken cancellationToken = default);
}
