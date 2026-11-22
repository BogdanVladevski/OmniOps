using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Infrastructure.Persistence;

namespace OmniOps.Infrastructure.Identity;

/// <summary>
/// Checked on every signed-in request. The account's state is cached briefly; ending sessions clears the entry, so a
/// removed teammate or an old password stops working at once (single instance; with several, the cache window applies).
/// </summary>
internal sealed class AccountSessions(AppDbContext db, IMemoryCache cache, TimeProvider clock) : IAccountSessions
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(30);

    public async Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        await db.RefreshTokens.Where(t => t.UserId == userId && !t.Revoked)
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.Revoked, true), cancellationToken);
        await db.Users.IgnoreQueryFilters().Where(u => u.Id == userId)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.SessionsValidFromUtc, now), cancellationToken);
        cache.Remove(Key(userId));
    }

    public async Task<bool> IsCurrentAsync(Guid userId, DateTime issuedAtUtc, CancellationToken cancellationToken = default)
    {
        var state = await cache.GetOrCreateAsync(Key(userId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheFor;
            return await db.Users.IgnoreQueryFilters().AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new State(u.IsActive, u.SessionsValidFromUtc))
                .FirstOrDefaultAsync(cancellationToken);
        });

        // Tokens carry milliseconds: compare at that precision.
        return state is { IsActive: true }
            && (state.ValidFromUtc is not { } from || issuedAtUtc >= from.AddTicks(-(from.Ticks % TimeSpan.TicksPerMillisecond)));
    }

    private static string Key(Guid userId) => $"account-session:{userId:N}";

    private sealed record State(bool IsActive, DateTime? ValidFromUtc);
}
