namespace OmniOps.Application.Dtos;

public record ApiKeyDto(Guid Id, string Name, string KeyPrefix, string Scopes, DateTime CreatedAtUtc, DateTime? ExpiresAtUtc);

public record AuditLogDto(
    Guid Id,
    string Action,
    string EntityType,
    string? EntityId,
    string? UserId,
    string? Details,
    DateTime OccurredAtUtc);

public record CreateApiKeyResponse(Guid Id, string Name, string ApiKey, string KeyPrefix, DateTime? ExpiresAtUtc);
