using OmniOps.Core.Entities;

namespace OmniOps.Core.Interfaces;

public interface IAuditLogRepository
{
    Task<IReadOnlyList<AuditLog>> QueryAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? entityType,
        int limit,
        CancellationToken cancellationToken = default);
}
