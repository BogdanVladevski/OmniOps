using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OmniOps.Application.Commands;
using OmniOps.Application.Commands.Handlers;
using OmniOps.Application.Queries;
using OmniOps.Application.Queries.Handlers;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Tests;

public class ProcessTelemetryCommandHandlerTests
{
    private readonly ITelemetryRepository _repository = Substitute.For<ITelemetryRepository>();
    private readonly ITelemetryCacheService _cacheService = Substitute.For<ITelemetryCacheService>();
    private readonly ITelemetryBroadcastService _broadcastService = Substitute.For<ITelemetryBroadcastService>();
    private readonly IDeduplicationService _deduplicationService = Substitute.For<IDeduplicationService>();
    private readonly IAnomalyDetectionService _anomalyService = Substitute.For<IAnomalyDetectionService>();
    private readonly IPlaybookOrchestrationService _playbookOrchestration = Substitute.For<IPlaybookOrchestrationService>();
    private readonly ITelemetryMetrics _metrics = Substitute.For<ITelemetryMetrics>();
    private readonly ProcessTelemetryCommandHandler _handler;

    public ProcessTelemetryCommandHandlerTests()
    {
        _handler = new ProcessTelemetryCommandHandler(
            _repository,
            _cacheService,
            _broadcastService,
            _deduplicationService,
            _anomalyService,
            _playbookOrchestration,
            _metrics,
            NullLogger<ProcessTelemetryCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_WhenDedupLockNotAcquired_SkipsPersistenceAndReturnsTrue()
    {
        var telemetry = CreateTelemetry();
        _deduplicationService.TryAcquireProcessingLockAsync(telemetry.Id, Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await _handler.Handle(new ProcessTelemetryCommand(telemetry), CancellationToken.None);

        Assert.True(result);
        await _repository.DidNotReceive().AddAsync(Arg.Any<VehicleTelemetry>(), Arg.Any<CancellationToken>());
        await _broadcastService.DidNotReceive()
            .BroadcastTelemetryUpdateAsync(Arg.Any<VehicleTelemetry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSuccessful_PersistsCachesBroadcastsAndReturnsTrue()
    {
        var telemetry = CreateTelemetry();
        _deduplicationService.TryAcquireProcessingLockAsync(telemetry.Id, Arg.Any<CancellationToken>())
            .Returns(true);
        _anomalyService.AnalyzeTelemetryAsync(telemetry, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _handler.Handle(new ProcessTelemetryCommand(telemetry), CancellationToken.None);

        Assert.True(result);
        await _repository.Received(1).AddAsync(telemetry, Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _cacheService.Received(1).SetLatestTelemetryAsync(telemetry);
        await _broadcastService.Received(1)
            .BroadcastTelemetryUpdateAsync(telemetry, Arg.Any<CancellationToken>());
        await _playbookOrchestration.DidNotReceive()
            .OrchestrateIncidentResponseAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAnomalyDetected_TriggersPlaybookOrchestration()
    {
        var telemetry = CreateTelemetry();
        _deduplicationService.TryAcquireProcessingLockAsync(telemetry.Id, Arg.Any<CancellationToken>())
            .Returns(true);
        _anomalyService.AnalyzeTelemetryAsync(telemetry, Arg.Any<CancellationToken>()).Returns(true);

        await _handler.Handle(new ProcessTelemetryCommand(telemetry), CancellationToken.None);

        await _playbookOrchestration.Received(1)
            .OrchestrateIncidentResponseAsync(
                telemetry.VehicleId,
                Arg.Is<string>(s => s.Contains(telemetry.VehicleId)),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenPersistenceFails_DoesNotUpdateCacheOrBroadcast()
    {
        var telemetry = CreateTelemetry();
        _deduplicationService.TryAcquireProcessingLockAsync(telemetry.Id, Arg.Any<CancellationToken>())
            .Returns(true);
        _repository.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("db error"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(new ProcessTelemetryCommand(telemetry), CancellationToken.None));

        await _cacheService.DidNotReceive().SetLatestTelemetryAsync(Arg.Any<VehicleTelemetry>());
        await _broadcastService.DidNotReceive()
            .BroadcastTelemetryUpdateAsync(Arg.Any<VehicleTelemetry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenCacheFails_StillBroadcasts()
    {
        var telemetry = CreateTelemetry();
        _deduplicationService.TryAcquireProcessingLockAsync(telemetry.Id, Arg.Any<CancellationToken>())
            .Returns(true);
        _anomalyService.AnalyzeTelemetryAsync(telemetry, Arg.Any<CancellationToken>()).Returns(false);
        _cacheService.SetLatestTelemetryAsync(telemetry)
            .Returns<Task>(_ => throw new InvalidOperationException("redis down"));

        var result = await _handler.Handle(new ProcessTelemetryCommand(telemetry), CancellationToken.None);

        Assert.True(result);
        await _broadcastService.Received(1)
            .BroadcastTelemetryUpdateAsync(telemetry, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenPacketIdEmpty_AssignsNewGuidBeforeDedup()
    {
        var telemetry = CreateTelemetry();
        telemetry.Id = Guid.Empty;

        _deduplicationService.TryAcquireProcessingLockAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _anomalyService.AnalyzeTelemetryAsync(telemetry, Arg.Any<CancellationToken>()).Returns(false);

        await _handler.Handle(new ProcessTelemetryCommand(telemetry), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, telemetry.Id);
        await _deduplicationService.Received(1)
            .TryAcquireProcessingLockAsync(telemetry.Id, Arg.Any<CancellationToken>());
    }

    private static VehicleTelemetry CreateTelemetry() => new()
    {
        Id = Guid.NewGuid(),
        VehicleId = "Truck-001",
        Latitude = 41.99,
        Longitude = 21.43,
        Speed = 80,
        FuelLevel = 75,
        EngineTemperature = 90,
        Timestamp = DateTime.UtcNow
    };
}

public class GetLatestTelemetryQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenCacheHit_ReturnsMappedDto()
    {
        var cache = Substitute.For<ITelemetryCacheService>();
        var handler = new GetLatestTelemetryQueryHandler(cache);
        var entity = new VehicleTelemetry
        {
            Id = Guid.NewGuid(),
            VehicleId = "Truck-001",
            Latitude = 41.99,
            Longitude = 21.43,
            Speed = 80,
            FuelLevel = 75,
            EngineTemperature = 90,
            Timestamp = DateTime.UtcNow
        };
        cache.GetLatestTelemetryAsync("Truck-001").Returns(entity);

        var result = await handler.Handle(new GetLatestTelemetryQuery("Truck-001"), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(entity.VehicleId, result!.VehicleId);
        Assert.Equal(entity.Latitude, result.Latitude);
    }

    [Fact]
    public async Task Handle_WhenCacheMiss_ReturnsNull()
    {
        var cache = Substitute.For<ITelemetryCacheService>();
        var handler = new GetLatestTelemetryQueryHandler(cache);
        cache.GetLatestTelemetryAsync("Truck-001").Returns((VehicleTelemetry?)null);

        var result = await handler.Handle(new GetLatestTelemetryQuery("Truck-001"), CancellationToken.None);

        Assert.Null(result);
    }
}

public class GetFleetTelemetryQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsActiveVehiclesAndSummary()
    {
        var cache = Substitute.For<ITelemetryCacheService>();
        var registry = Substitute.For<IFleetVehicleRegistry>();
        registry.GetConfiguredVehicleIds().Returns(["Truck-001", "Truck-002"]);

        cache.GetLatestTelemetryAsync("Truck-001").Returns(new VehicleTelemetry
        {
            VehicleId = "Truck-001",
            FuelLevel = 80,
            EngineTemperature = 90,
            Latitude = 41.99,
            Longitude = 21.43,
            Speed = 70,
            Timestamp = DateTime.UtcNow
        });
        cache.GetLatestTelemetryAsync("Truck-002").Returns((VehicleTelemetry?)null);

        var handler = new GetFleetTelemetryQueryHandler(cache, registry);
        var result = await handler.Handle(new GetFleetTelemetryQuery(), CancellationToken.None);

        Assert.Equal(2, result.Summary.ConfiguredVehicleCount);
        Assert.Equal(1, result.Summary.ActiveVehicleCount);
        Assert.Single(result.Vehicles);
        Assert.Equal("Truck-001", result.Vehicles[0].VehicleId);
        Assert.Equal(80, result.Summary.AverageFuelLevel);
    }

    [Fact]
    public async Task Handle_CountsVehiclesInWarningState()
    {
        var cache = Substitute.For<ITelemetryCacheService>();
        var registry = Substitute.For<IFleetVehicleRegistry>();
        registry.GetConfiguredVehicleIds().Returns(["Truck-001"]);

        cache.GetLatestTelemetryAsync("Truck-001").Returns(new VehicleTelemetry
        {
            VehicleId = "Truck-001",
            FuelLevel = 20,
            EngineTemperature = 95,
            Latitude = 41.99,
            Longitude = 21.43,
            Speed = 40,
            Timestamp = DateTime.UtcNow
        });

        var handler = new GetFleetTelemetryQueryHandler(cache, registry);
        var result = await handler.Handle(new GetFleetTelemetryQuery(), CancellationToken.None);

        Assert.Equal(1, result.Summary.WarningCount);
    }
}
