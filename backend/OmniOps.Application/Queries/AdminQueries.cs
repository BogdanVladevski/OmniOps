using MediatR;
using OmniOps.Application.Abstractions;
using OmniOps.Application.Dtos;
using OmniOps.Core.Authorization;

namespace OmniOps.Application.Queries;

public record GetAuditLogsQuery(DateTime? FromUtc, DateTime? ToUtc, string? EntityType, int Limit = 100)
    : IRequest<IReadOnlyList<AuditLogDto>>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.PlatformAdmin;
}

public record GetApiKeysQuery() : IRequest<IReadOnlyList<ApiKeyDto>>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.PlatformAdmin;
}
