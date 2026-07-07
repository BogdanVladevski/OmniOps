using MediatR;
using OmniOps.Application.Abstractions;
using OmniOps.Application.Dtos;
using OmniOps.Core.Authorization;

namespace OmniOps.Application.Commands;

public record CreateApiKeyCommand(string Name, string Scopes, int? ExpiresInDays)
    : IRequest<CreateApiKeyResponse>, IAuthorizedRequest, ITransactionalRequest
{
    public string RequiredPolicy => AuthorizationPolicies.PlatformAdmin;
}

public record RevokeApiKeyCommand(Guid ApiKeyId) : IRequest<bool>, IAuthorizedRequest, ITransactionalRequest
{
    public string RequiredPolicy => AuthorizationPolicies.PlatformAdmin;
}
