using OmniOps.Infrastructure.Services;

namespace OmniOps.Infrastructure.Tests.Services;

public class InMemoryDeduplicationServiceTests
{
    [Fact]
    public async Task TryAcquireProcessingLockAsync_WithSamePacketId_SecondCallReturnsFalse()
    {
        var service = new InMemoryDeduplicationService();
        var packetId = Guid.NewGuid();

        Assert.True(await service.TryAcquireProcessingLockAsync(packetId));
        Assert.False(await service.TryAcquireProcessingLockAsync(packetId));
    }

    [Fact]
    public async Task TryAcquireProcessingLockAsync_WithDifferentPacketIds_BothReturnTrue()
    {
        var service = new InMemoryDeduplicationService();

        Assert.True(await service.TryAcquireProcessingLockAsync(Guid.NewGuid()));
        Assert.True(await service.TryAcquireProcessingLockAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ReleaseProcessingLockAsync_AllowsRetryOfSamePacket()
    {
        var service = new InMemoryDeduplicationService();
        var packetId = Guid.NewGuid();

        await service.TryAcquireProcessingLockAsync(packetId);
        await service.ReleaseProcessingLockAsync(packetId);

        Assert.True(await service.TryAcquireProcessingLockAsync(packetId));
    }

    [Fact]
    public async Task TryAcquireProcessingLockAsync_AfterLockExpires_AllowsExactlyOneTakeover()
    {
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var service = new InMemoryDeduplicationService(clock);
        var packetId = Guid.NewGuid();

        await service.TryAcquireProcessingLockAsync(packetId);
        clock.Advance(InMemoryDeduplicationService.LockExpiration + TimeSpan.FromSeconds(1));

        Assert.True(await service.TryAcquireProcessingLockAsync(packetId));
        Assert.False(await service.TryAcquireProcessingLockAsync(packetId));
    }

    [Fact]
    public async Task TryAcquireProcessingLockAsync_ConcurrentCallers_OnlyOneWins()
    {
        var service = new InMemoryDeduplicationService();
        var packetId = Guid.NewGuid();

        var results = await Task.WhenAll(Enumerable.Range(0, 50)
            .Select(_ => Task.Run(() => service.TryAcquireProcessingLockAsync(packetId))));

        Assert.Equal(1, results.Count(r => r));
    }
}

internal sealed class ManualClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
