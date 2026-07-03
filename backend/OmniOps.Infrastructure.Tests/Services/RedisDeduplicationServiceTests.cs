using Microsoft.Extensions.Logging.Abstractions;
using OmniOps.Infrastructure.Services;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace OmniOps.Infrastructure.Tests.Services;

public class RedisDeduplicationServiceTests : IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    private IConnectionMultiplexer _connection = null!;

    public async Task InitializeAsync()
    {
        await _redis.StartAsync();
        _connection = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        await _redis.DisposeAsync();
    }

    [Fact]
    public async Task TryAcquireProcessingLockAsync_WithSamePacketId_SecondCallReturnsFalse()
    {
        var service = new RedisDeduplicationService(
            _connection,
            NullLogger<RedisDeduplicationService>.Instance);

        var packetId = Guid.NewGuid();

        var first = await service.TryAcquireProcessingLockAsync(packetId);
        var second = await service.TryAcquireProcessingLockAsync(packetId);

        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public async Task TryAcquireProcessingLockAsync_WithDifferentPacketIds_BothReturnTrue()
    {
        var service = new RedisDeduplicationService(
            _connection,
            NullLogger<RedisDeduplicationService>.Instance);

        var first = await service.TryAcquireProcessingLockAsync(Guid.NewGuid());
        var second = await service.TryAcquireProcessingLockAsync(Guid.NewGuid());

        Assert.True(first);
        Assert.True(second);
    }

    [Fact]
    public async Task ReleaseProcessingLockAsync_AllowsSubsequentLockAcquisition()
    {
        var service = new RedisDeduplicationService(
            _connection,
            NullLogger<RedisDeduplicationService>.Instance);

        var packetId = Guid.NewGuid();

        Assert.True(await service.TryAcquireProcessingLockAsync(packetId));
        Assert.False(await service.TryAcquireProcessingLockAsync(packetId));

        await service.ReleaseProcessingLockAsync(packetId);

        Assert.True(await service.TryAcquireProcessingLockAsync(packetId));
    }
}
