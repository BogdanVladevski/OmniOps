using System.Security.Cryptography;
using System.Text;
using MediatR;
using OmniOps.Application.Commands;
using OmniOps.Application.Dtos;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Commands.Handlers;

public class CreateApiKeyCommandHandler : IRequestHandler<CreateApiKeyCommand, CreateApiKeyResponse>
{
    private readonly IApiKeyRepository _apiKeys;
    private readonly ITenantContext _tenant;
    private readonly IAuditService _audit;

    public CreateApiKeyCommandHandler(IApiKeyRepository apiKeys, ITenantContext tenant, IAuditService audit)
    {
        _apiKeys = apiKeys;
        _tenant = tenant;
        _audit = audit;
    }

    public async Task<CreateApiKeyResponse> Handle(CreateApiKeyCommand request, CancellationToken cancellationToken)
    {
        var rawKey = $"omni_{Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)).TrimEnd('=').Replace('+', '-').Replace('/', '_')}";
        var prefix = rawKey[..12];
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey)));
        var entity = new ApiKey
        {
            Id = Guid.NewGuid(),
            OrganizationId = _tenant.OrganizationId,
            Name = request.Name.Trim(),
            KeyHash = hash,
            KeyPrefix = prefix,
            Scopes = request.Scopes,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = request.ExpiresInDays is int days and > 0
                ? DateTime.UtcNow.AddDays(days)
                : null
        };
        await _apiKeys.AddAsync(entity, cancellationToken);
        await _audit.LogAsync("Create", nameof(ApiKey), entity.Id.ToString(), _tenant.UserId, entity.Name, cancellationToken);
        await _apiKeys.SaveChangesAsync(cancellationToken);
        return new CreateApiKeyResponse(entity.Id, entity.Name, rawKey, prefix, entity.ExpiresAtUtc);
    }
}

public class RevokeApiKeyCommandHandler : IRequestHandler<RevokeApiKeyCommand, bool>
{
    private readonly IApiKeyRepository _apiKeys;

    public RevokeApiKeyCommandHandler(IApiKeyRepository apiKeys) => _apiKeys = apiKeys;

    public async Task<bool> Handle(RevokeApiKeyCommand request, CancellationToken cancellationToken)
    {
        await _apiKeys.RevokeAsync(request.ApiKeyId, cancellationToken);
        await _apiKeys.SaveChangesAsync(cancellationToken);
        return true;
    }
}
