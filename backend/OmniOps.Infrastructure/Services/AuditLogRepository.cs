using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Data;

namespace OmniOps.Infrastructure.Services;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly AppDbContext _context;

    public AuditLogRepository(AppDbContext context) => _context = context;

    public async Task<IReadOnlyList<AuditLog>> QueryAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? entityType,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AuditLogs.AsNoTracking().AsQueryable();
        if (fromUtc is not null) query = query.Where(l => l.OccurredAtUtc >= fromUtc);
        if (toUtc is not null) query = query.Where(l => l.OccurredAtUtc <= toUtc);
        if (!string.IsNullOrWhiteSpace(entityType)) query = query.Where(l => l.EntityType == entityType);
        return await query.OrderByDescending(l => l.OccurredAtUtc).Take(limit).ToListAsync(cancellationToken);
    }
}
