namespace OmniOps.Core.Interfaces;

/// <summary>Ends sign-ins: refresh tokens are revoked and access tokens issued earlier stop working.</summary>
public interface IAccountSessions
{
    /// <summary>Call after the account change is saved.</summary>
    Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>False if the account was switched off or its sessions were ended after the token was issued.</summary>
    Task<bool> IsCurrentAsync(Guid userId, DateTime issuedAtUtc, CancellationToken cancellationToken = default);
}
