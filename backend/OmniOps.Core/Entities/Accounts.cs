using OmniOps.Core.Enums;

namespace OmniOps.Core.Entities;

/// <summary>Someone who signs in: office staff with email + password, or a driver paired by code.</summary>
public class UserAccount
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }

    /// <summary>Lower-cased email; unique across all organizations. Null for drivers.</summary>
    public string? Email { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>PBKDF2 hash (see IPasswordHasher). Null for drivers, who sign in with a pairing code.</summary>
    public string? PasswordHash { get; set; }

    public UserRole Role { get; set; }

    /// <summary>For drivers: the fleet driver this account acts as (they only ever see that driver's jobs).</summary>
    public Guid? DriverId { get; set; }

    public bool IsActive { get; set; } = true;
    public int FailedSignIns { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastSignInAtUtc { get; set; }

    /// <summary>Access tokens issued before this moment are refused (password reset, removal, "sign out everywhere").</summary>
    public DateTime? SessionsValidFromUtc { get; set; }
}

/// <summary>One-time code the office gives a driver to sign in their phone. Only a hash is stored.</summary>
public class DriverPairingCode
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid DriverId { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// A one-time link sent to someone: an invitation to join a company, or a password reset.
/// Only a hash of the token is stored.
/// </summary>
public class AccountLink
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public AccountLinkPurpose Purpose { get; set; }

    /// <summary>Lower-cased email the link was made for.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Invitations: the role the person gets when they accept.</summary>
    public UserRole Role { get; set; }

    /// <summary>Password resets: whose password the link changes.</summary>
    public Guid? UserId { get; set; }

    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public bool IsOpen(DateTime now) => UsedAtUtc is null && RevokedAtUtc is null && ExpiresAtUtc > now;
}
