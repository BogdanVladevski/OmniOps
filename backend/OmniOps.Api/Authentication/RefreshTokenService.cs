using Microsoft.EntityFrameworkCore;
using OmniOps.Core.Entities;
using OmniOps.Infrastructure.Data;
using System.Security.Cryptography;
using System.Text;

namespace OmniOps.Api.Authentication;

public interface IRefreshTokenService
{
    Task<string> IssueAsync(string userId, CancellationToken cancellationToken = default);
    Task<string?> RotateAsync(string refreshToken, CancellationToken cancellationToken = default);
}

public class RefreshTokenService : IRefreshTokenService
{
    private readonly AppDbContext _context;

    public RefreshTokenService(AppDbContext context) => _context = context;

    public async Task<string> IssueAsync(string userId, CancellationToken cancellationToken = default)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        await _context.RefreshTokens.AddAsync(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = Hash(token),
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
        }, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return token;
    }

    public async Task<string?> RotateAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var hash = Hash(refreshToken);
        var stored = await _context.RefreshTokens.FirstOrDefaultAsync(
            t => t.TokenHash == hash && !t.Revoked && t.ExpiresAtUtc > DateTime.UtcNow, cancellationToken);
        if (stored is null) return null;
        stored.Revoked = true;
        return await IssueAsync(stored.UserId, cancellationToken);
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
