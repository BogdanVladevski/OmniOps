using MediatR;
using OmniOps.Application.Dtos;
using OmniOps.Application.Queries;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Queries.Handlers;

public class GetAuditLogsQueryHandler : IRequestHandler<GetAuditLogsQuery, IReadOnlyList<AuditLogDto>>
{
    private readonly IAuditLogRepository _auditLogs;
    private readonly ITenantContext _tenant;

    public GetAuditLogsQueryHandler(IAuditLogRepository auditLogs, ITenantContext tenant)
    {
        _auditLogs = auditLogs;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<AuditLogDto>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var logs = await _auditLogs.QueryAsync(
            _tenant.OrganizationId,
            request.FromUtc, request.ToUtc, request.EntityType, request.Limit, cancellationToken);

        return logs.Select(l => new AuditLogDto(
            l.Id, l.Action, l.EntityType, l.EntityId, l.UserId, l.Details, l.OccurredAtUtc)).ToList();
    }
}

public class GetApiKeysQueryHandler : IRequestHandler<GetApiKeysQuery, IReadOnlyList<ApiKeyDto>>
{
    private readonly IApiKeyRepository _apiKeys;
    private readonly ITenantContext _tenant;

    public GetApiKeysQueryHandler(IApiKeyRepository apiKeys, ITenantContext tenant)
    {
        _apiKeys = apiKeys;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<ApiKeyDto>> Handle(GetApiKeysQuery request, CancellationToken cancellationToken)
    {
        var keys = await _apiKeys.GetByOrganizationAsync(_tenant.OrganizationId, cancellationToken);
        return keys.Where(k => !k.Revoked).Select(k => new ApiKeyDto(
            k.Id, k.Name, k.KeyPrefix, k.Scopes, k.CreatedAtUtc, k.ExpiresAtUtc)).ToList();
    }
}
