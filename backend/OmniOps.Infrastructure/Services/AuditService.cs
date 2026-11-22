using Microsoft.EntityFrameworkCore;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Data;

namespace OmniOps.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly AppDbContext _context;
    private readonly ITenantContext _tenant;

    public AuditService(AppDbContext context, ITenantContext tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    public Task LogAsync(
        string action, string entityType, string? entityId, string? userId, string? details,
        CancellationToken cancellationToken = default) =>
        LogForAsync(_tenant.OrganizationId, action, entityType, entityId, userId, details, cancellationToken);

    public async Task LogForAsync(
        Guid organizationId, string action, string entityType, string? entityId, string? userId, string? details,
        CancellationToken cancellationToken = default)
    {
        await _context.AuditLogs.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            UserId = userId,
            Details = details,
            OccurredAtUtc = DateTime.UtcNow
        }, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
