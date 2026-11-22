using Microsoft.EntityFrameworkCore;
using OmniOps.Core.Entities;
using OmniOps.Infrastructure.Data;
using System.Security.Cryptography;
using System.Text;

namespace OmniOps.Api.Authentication;

public interface IRefreshTokenService
{
    Task<string> IssueAsync(string userId, CancellationToken cancellationToken = default, TimeSpan? lifetime = null);

    /// <summary>Single use: the old token is revoked and a new one (same lifetime) issued. Null if invalid.</summary>
    Task<(string Token, string UserId)?> RotateAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default);
}

public class RefreshTokenService : IRefreshTokenService
{
    private readonly AppDbContext _context;

    public RefreshTokenService(AppDbContext context) => _context = context;

    public async Task<string> IssueAsync(string userId, CancellationToken cancellationToken = default, TimeSpan? lifetime = null)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        await _context.RefreshTokens.AddAsync(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = Hash(token),
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromDays(7))
        }, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return token;
    }

    public async Task<(string Token, string UserId)?> RotateAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var hash = Hash(refreshToken);
        var stored = await _context.RefreshTokens.FirstOrDefaultAsync(
            t => t.TokenHash == hash && !t.Revoked && t.ExpiresAtUtc > DateTime.UtcNow, cancellationToken);
        if (stored is null) return null;
        stored.Revoked = true;
        var lifetime = stored.ExpiresAtUtc - stored.CreatedAtUtc;
        return (await IssueAsync(stored.UserId, cancellationToken, lifetime), stored.UserId);
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var hash = Hash(refreshToken);
        var stored = await _context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (stored is null) return;
        stored.Revoked = true;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
