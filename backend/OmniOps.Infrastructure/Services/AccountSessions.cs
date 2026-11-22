using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OmniOps.Infrastructure.Data;
using OmniOps.Core.Interfaces;

namespace OmniOps.Infrastructure.Services;

/// <summary>
/// Checked on every signed-in request. The account's state is cached briefly; ending sessions clears the entry,
/// so a removed teammate or an old password stops working at once on this (single) instance.
/// </summary>
public class AccountSessions : IAccountSessions
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(30);

    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _clock;

    public AccountSessions(AppDbContext context, IMemoryCache cache, TimeProvider clock)
    {
        _context = context;
        _cache = cache;
        _clock = clock;
    }

    public async Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var id = userId.ToString();
        var now = _clock.GetUtcNow().UtcDateTime;
        await _context.RefreshTokens
            .Where(t => t.UserId == id && !t.Revoked)
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.Revoked, true), cancellationToken);
        await _context.UserAccounts
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.SessionsValidFromUtc, now), cancellationToken);
        _cache.Remove(Key(userId));
    }

    public async Task<bool> IsCurrentAsync(Guid userId, DateTime issuedAtUtc, CancellationToken cancellationToken = default)
    {
        var state = await _cache.GetOrCreateAsync(Key(userId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheFor;
            return await _context.UserAccounts.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new State(u.IsActive, u.SessionsValidFromUtc))
                .FirstOrDefaultAsync(cancellationToken);
        });

        if (state is null)
        {
            return false;
        }

        // Tokens carry milliseconds: compare at that precision.
        return state.IsActive && (state.ValidFromUtc is not { } from || issuedAtUtc >= from.AddTicks(-(from.Ticks % TimeSpan.TicksPerMillisecond)));
    }

    private static string Key(Guid userId) => $"account-session:{userId:N}";

    private sealed record State(bool IsActive, DateTime? ValidFromUtc);
}
