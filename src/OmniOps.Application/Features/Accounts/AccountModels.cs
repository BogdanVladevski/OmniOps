using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Domain.Accounts;

namespace OmniOps.Application.Features.Accounts;

/// <summary>Who is signed in. The API turns this into tokens; the app uses it to choose office or driver mode.</summary>
public sealed record AccountDto(
    Guid UserId, string DisplayName, string? Email, string Role, Guid OrganizationId, string OrganizationName, Guid? DriverId);

public sealed record DriverPairingCodeDto(string Code, DateTime ExpiresAtUtc, string DriverName);

public sealed record TeamMemberDto(Guid UserId, string DisplayName, string? Email, string Role, bool IsActive, DateTime? LastSignInAtUtc, bool IsYou);

/// <summary>An invitation not yet accepted. The link is only known when it is created.</summary>
public sealed record TeamInvitationDto(Guid Id, string Email, string Role, DateTime ExpiresAtUtc, string? Link, bool Emailed);

public sealed record TeamDto(IReadOnlyList<TeamMemberDto> Members, IReadOnlyList<TeamInvitationDto> Invitations, bool EmailConfigured);

/// <summary>What the person opening an invitation link sees before choosing a password.</summary>
public sealed record InvitationPreviewDto(string CompanyName, string Email, string Role);

/// <summary>A one-time link (password reset) to hand over when no email could be sent.</summary>
public sealed record AccountLinkDto(string Link, DateTime ExpiresAtUtc, bool Emailed);

public sealed record CompanyDto(Guid OrganizationId, string Name);

internal static partial class AccountRules
{
    public const int MinPasswordLength = 10;
    public const int MaxFailedSignIns = 5;
    public static readonly TimeSpan Lockout = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan PairingCodeLifetime = TimeSpan.FromHours(24);
    public static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);
    public static readonly TimeSpan PasswordResetLifetime = TimeSpan.FromHours(1);

    // No 0/O, 1/I/L: codes are read aloud and typed on a phone.
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static string NewPairingCode()
    {
        Span<char> chars = stackalloc char[8];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
        }

        return $"{chars[..4]}-{chars[4..]}";
    }

    /// <summary>"k7qx m2pd", "K7QX-M2PD" and "k7qxm2pd" are the same code.</summary>
    public static string NormalizeCode(string code) => NonCode().Replace(code.ToUpperInvariant(), string.Empty);

    /// <summary>Unguessable token for a link (URL-safe).</summary>
    public static string NewLinkToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public static string Slugify(string name)
    {
        var slug = NonSlug().Replace(name.Trim().ToLowerInvariant(), "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? "company" : slug[..Math.Min(slug.Length, 40)];
    }

    public static bool IsOfficeRole(string role) =>
        string.Equals(role, nameof(UserRole.Owner), StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, nameof(UserRole.Dispatcher), StringComparison.OrdinalIgnoreCase);

    public static async Task<AccountDto> ToDtoAsync(this UserAccount user, IAppDbContext db, CancellationToken ct)
    {
        var company = await db.Organizations.Where(o => o.Id == user.OrganizationId).Select(o => o.Name).FirstAsync(ct);
        return new AccountDto(user.Id, user.DisplayName, user.Email, user.Role.ToString(), user.OrganizationId, company, user.DriverId);
    }

    [GeneratedRegex("[^A-Z0-9]")]
    private static partial Regex NonCode();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlug();
}
