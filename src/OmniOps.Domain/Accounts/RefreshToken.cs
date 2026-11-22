namespace OmniOps.Domain.Accounts;

/// <summary>A long-lived sign-in, renewed with single-use tokens. Only a hash is stored.</summary>
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public bool Revoked { get; set; }
}
