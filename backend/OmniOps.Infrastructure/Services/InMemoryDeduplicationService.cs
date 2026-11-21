using System.Collections.Concurrent;
using OmniOps.Core.Interfaces;

namespace OmniOps.Infrastructure.Services;

/// <summary>
/// Per-packet processing lock held in process memory. Correct for a single API instance; a restart
/// forgets recent packets, which at worst re-processes a duplicate (tolerated — persistence is keyed by packet id).
/// Must be registered as a singleton.
/// </summary>
public class InMemoryDeduplicationService : IDeduplicationService
{
    public static readonly TimeSpan LockExpiration = TimeSpan.FromMinutes(10);
    private const int SweepEvery = 1024;

    private readonly ConcurrentDictionary<Guid, DateTime> _locks = new();
    private readonly TimeProvider _clock;
    private int _acquisitions;

    public InMemoryDeduplicationService(TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;
    }

    public Task<bool> TryAcquireProcessingLockAsync(Guid packetId, CancellationToken cancellationToken = default)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var expiresAt = now + LockExpiration;

        if (Interlocked.Increment(ref _acquisitions) % SweepEvery == 0)
        {
            SweepExpired(now);
        }

        if (_locks.TryAdd(packetId, expiresAt))
        {
            return Task.FromResult(true);
        }

        // An expired lock may be taken over, but only by one caller.
        if (_locks.TryGetValue(packetId, out var existing) && existing <= now)
        {
            return Task.FromResult(_locks.TryUpdate(packetId, expiresAt, existing));
        }

        return Task.FromResult(false);
    }

    public Task ReleaseProcessingLockAsync(Guid packetId, CancellationToken cancellationToken = default)
    {
        _locks.TryRemove(packetId, out _);
        return Task.CompletedTask;
    }

    private void SweepExpired(DateTime now)
    {
        foreach (var (id, expiresAt) in _locks)
        {
            if (expiresAt <= now)
            {
                _locks.TryRemove(new KeyValuePair<Guid, DateTime>(id, expiresAt));
            }
        }
    }
}
