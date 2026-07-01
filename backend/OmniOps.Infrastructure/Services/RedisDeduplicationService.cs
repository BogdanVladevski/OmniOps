using Microsoft.Extensions.Logging;
using OmniOps.Core.Interfaces;
using StackExchange.Redis;

namespace OmniOps.Infrastructure.Services;

public class RedisDeduplicationService : IDeduplicationService
{
    private readonly IConnectionMultiplexer _redisConnection;
    private readonly ILogger<RedisDeduplicationService> _logger;
    private static readonly TimeSpan LockExpiration = TimeSpan.FromMinutes(10);

    public RedisDeduplicationService(
        IConnectionMultiplexer redisConnection,
        ILogger<RedisDeduplicationService> logger)
    {
        _redisConnection = redisConnection;
        _logger = logger;
    }

    public async Task<bool> TryAcquireProcessingLockAsync(
        Guid packetId,
        CancellationToken cancellationToken = default)
    {
        var db = _redisConnection.GetDatabase();
        var dedupKey = $"telemetry:dedup:{packetId}";

        try
        {
            return await db.StringSetAsync(dedupKey, "1", LockExpiration, When.NotExists);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Redis deduplication check failed for PacketId={PacketId}. Proceeding with processing",
                packetId);
            return true;
        }
    }
}
