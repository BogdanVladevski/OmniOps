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
