using Mediator;
using Microsoft.EntityFrameworkCore;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Application.Common.Security;

namespace OmniOps.Application.Features.Audit;

public sealed record AuditEntryDto(Guid Id, string Action, string EntityType, string? EntityId, Guid? UserId, string? UserName, string? Details, DateTime OccurredAtUtc);

/// <summary>Owners: who changed what in their company, newest first.</summary>
[RequiresPolicy(Policies.Owner)]
public sealed record GetAuditLogQuery(int Limit = 100) : IQuery<IReadOnlyList<AuditEntryDto>>;

public sealed class GetAuditLogHandler(IAppDbContext db) : IQueryHandler<GetAuditLogQuery, IReadOnlyList<AuditEntryDto>>
{
    public async ValueTask<IReadOnlyList<AuditEntryDto>> Handle(GetAuditLogQuery query, CancellationToken cancellationToken)
    {
        var entries = await db.AuditEntries.OrderByDescending(a => a.OccurredAtUtc).Take(Math.Clamp(query.Limit, 1, 500)).ToListAsync(cancellationToken);
        var userIds = entries.Select(e => e.UserId).OfType<Guid>().Distinct().ToList();
        var names = await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);
        return entries.Select(e => new AuditEntryDto(e.Id, e.Action, e.EntityType, e.EntityId, e.UserId,
            e.UserId is { } u && names.TryGetValue(u, out var name) ? name : null, e.Details, e.OccurredAtUtc)).ToList();
    }
}
