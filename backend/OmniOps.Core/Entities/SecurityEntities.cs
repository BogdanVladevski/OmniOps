namespace OmniOps.Core.Entities;

public class AuditLog
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? UserId { get; set; }
    public string? Details { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}

public class LoginHistory
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public bool Success { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}

public class RefreshToken
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public bool Revoked { get; set; }
}
