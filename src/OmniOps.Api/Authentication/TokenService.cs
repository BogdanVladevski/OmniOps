using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OmniOps.Application.Features.Accounts;
using OmniOps.Domain.Accounts;
using OmniOps.Infrastructure.Persistence;

namespace OmniOps.Api.Authentication;

public sealed record SessionDto(string AccessToken, string RefreshToken, int ExpiresIn, AccountDto Account);

/// <summary>Turns a signed-in account into an access token and a single-use refresh token.</summary>
public sealed class TokenService(AppDbContext db, IOptions<JwtOptions> options, TimeProvider clock)
{
    /// <summary>A driver's phone stays signed in for months; office sessions renew more often.</summary>
    private static readonly TimeSpan DriverSession = TimeSpan.FromDays(90);
    private static readonly TimeSpan OfficeSession = TimeSpan.FromDays(30);
    private readonly JwtOptions _options = options.Value;

    public static SymmetricSecurityKey SigningKey(JwtOptions options) => new(Encoding.UTF8.GetBytes(options.SigningKey));

    public async Task<SessionDto> IssueAsync(AccountDto account, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var claims = new Dictionary<string, object>
        {
            [OmniClaims.Subject] = account.UserId.ToString(),
            [OmniClaims.Organization] = account.OrganizationId.ToString(),
            [OmniClaims.Role] = account.Role,
            [OmniClaims.Name] = account.DisplayName,
            [OmniClaims.IssuedAtMs] = now.ToUnixTimeMilliseconds(),
        };
        if (account.DriverId is { } driverId)
        {
            claims[OmniClaims.Driver] = driverId.ToString();
        }

        var access = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Claims = claims,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = now.UtcDateTime.AddMinutes(_options.AccessTokenMinutes),
            SigningCredentials = new SigningCredentials(SigningKey(_options), SecurityAlgorithms.HmacSha256),
        });

        var lifetime = account.Role == nameof(UserRole.Driver) ? DriverSession : OfficeSession;
        var refresh = await IssueRefreshTokenAsync(account.UserId, lifetime, cancellationToken);
        return new SessionDto(access, refresh, _options.AccessTokenMinutes * 60, account);
    }

    /// <summary>Single use: the presented token is revoked. Returns the user it belonged to, or null if it is not valid.</summary>
    public async Task<Guid?> RedeemRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var hash = Hash(refreshToken);
        var now = clock.GetUtcNow().UtcDateTime;
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash && !t.Revoked && t.ExpiresAtUtc > now, cancellationToken);
        if (stored is null)
        {
            return null;
        }

        stored.Revoked = true;
        await db.SaveChangesAsync(cancellationToken);
        return stored.UserId;
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var hash = Hash(refreshToken);
        await db.RefreshTokens.Where(t => t.TokenHash == hash).ExecuteUpdateAsync(t => t.SetProperty(x => x.Revoked, true), cancellationToken);
    }

    private async Task<string> IssueRefreshTokenAsync(Guid userId, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var now = clock.GetUtcNow().UtcDateTime;
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(), UserId = userId, TokenHash = Hash(token), CreatedAtUtc = now, ExpiresAtUtc = now + lifetime,
        });
        await db.SaveChangesAsync(cancellationToken);
        return token;
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

public static class ClaimsPrincipalExtensions
{
    public static Guid? GuidClaim(this ClaimsPrincipal? user, string type) => Guid.TryParse(user?.FindFirst(type)?.Value, out var id) ? id : null;
}
