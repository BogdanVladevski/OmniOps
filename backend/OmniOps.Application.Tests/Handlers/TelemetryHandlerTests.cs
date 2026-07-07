using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OmniOps.Application.Commands;
using OmniOps.Application.Commands.Handlers;
using OmniOps.Application.Queries;
using OmniOps.Application.Queries.Handlers;
using OmniOps.Core.Entities;
using OmniOps.Core.Domain;
using OmniOps.Core.Exceptions;
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
    private readonly IShipmentRepository _shipmentRepository = Substitute.For<IShipmentRepository>();
    private readonly IGeofenceDetectionService _geofenceDetectionService = Substitute.For<IGeofenceDetectionService>();
    private readonly IIncidentDetectionService _incidentDetectionService = Substitute.For<IIncidentDetectionService>();
    private readonly IIncidentRepository _incidentRepository = Substitute.For<IIncidentRepository>();
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
            _shipmentRepository,
            _geofenceDetectionService,
            _incidentDetectionService,
            _incidentRepository,
            _metrics,
            NullLogger<ProcessTelemetryCommandHandler>.Instance);

        _geofenceDetectionService
            .DetectBreachesAsync(Arg.Any<VehicleTelemetry>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Core.Events.GeofenceBreachedEvent>());
        _incidentDetectionService
            .DetectAsync(Arg.Any<VehicleTelemetry>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Incident>());

        // Default: no active shipment for any vehicle.
        _shipmentRepository
            .GetActiveShipmentForVehicleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Shipment?)null);
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
        _anomalyService
            .AnalyzeTelemetryAsync(telemetry, Arg.Any<Shipment?>(), Arg.Any<CancellationToken>())
            .Returns(new AnomalyAnalysis(false));

        var result = await _handler.Handle(new ProcessTelemetryCommand(telemetry), CancellationToken.None);

        Assert.True(result);
        await _repository.Received(1).AddAsync(telemetry, Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _cacheService.Received(1).SetLatestTelemetryAsync(telemetry);
        await _broadcastService.Received(1)
            .BroadcastTelemetryUpdateAsync(telemetry, Arg.Any<CancellationToken>());
        await _playbookOrchestration.DidNotReceive()
            .OrchestrateIncidentResponseAsync(Arg.Any<IncidentContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAnomalyDetected_TriggersPlaybookOrchestration()
    {
        var telemetry = CreateTelemetry();
        _deduplicationService.TryAcquireProcessingLockAsync(telemetry.Id, Arg.Any<CancellationToken>())
            .Returns(true);
        _anomalyService
            .AnalyzeTelemetryAsync(telemetry, Arg.Any<Shipment?>(), Arg.Any<CancellationToken>())
            .Returns(new AnomalyAnalysis(true, ExcursionDurationSeconds: 45));

        await _handler.Handle(new ProcessTelemetryCommand(telemetry), CancellationToken.None);

        await _playbookOrchestration.Received(1)
            .OrchestrateIncidentResponseAsync(
                Arg.Is<IncidentContext>(c => c.VehicleId == telemetry.VehicleId
                    && c.IncidentSummary.Contains(telemetry.VehicleId)),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAnomalyDetectedWithActiveShipment_IncludesProductInIncidentSummary()
    {
        var telemetry = CreateTelemetry();
        var shipment = CreateShipment();
        _deduplicationService.TryAcquireProcessingLockAsync(telemetry.Id, Arg.Any<CancellationToken>())
            .Returns(true);
        _shipmentRepository
            .GetActiveShipmentForVehicleAsync(telemetry.VehicleId, Arg.Any<CancellationToken>())
            .Returns(shipment);
        _anomalyService
            .AnalyzeTelemetryAsync(telemetry, Arg.Any<Shipment?>(), Arg.Any<CancellationToken>())
            .Returns(new AnomalyAnalysis(true, ExcursionDurationSeconds: 90));

        await _handler.Handle(new ProcessTelemetryCommand(telemetry), CancellationToken.None);

        await _playbookOrchestration.Received(1)
            .OrchestrateIncidentResponseAsync(
                Arg.Is<IncidentContext>(c => c.ProductName == shipment.ProductName
                    && c.BatchNumber == shipment.BatchNumber
                    && c.IncidentSummary.Contains(shipment.ProductName)
                    && c.IncidentSummary.Contains(shipment.BatchNumber)),
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

        var thrown = await Assert.ThrowsAsync<TransientProcessingException>(() =>
            _handler.Handle(new ProcessTelemetryCommand(telemetry), CancellationToken.None));

        Assert.IsType<InvalidOperationException>(thrown.InnerException);
        await _deduplicationService.Received(1)
            .ReleaseProcessingLockAsync(telemetry.Id, Arg.Any<CancellationToken>());
        await _cacheService.DidNotReceive().SetLatestTelemetryAsync(Arg.Any<VehicleTelemetry>());
        await _broadcastService.DidNotReceive()
            .BroadcastTelemetryUpdateAsync(Arg.Any<VehicleTelemetry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenPersistenceFails_ReleasesDedupLockSoRetryCanProcess()
    {
        var telemetry = CreateTelemetry();
        var saveAttempts = 0;

        _deduplicationService.TryAcquireProcessingLockAsync(telemetry.Id, Arg.Any<CancellationToken>())
            .Returns(true);
        _repository.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                saveAttempts++;
                return saveAttempts == 1
                    ? Task.FromException(new InvalidOperationException("db error"))
                    : Task.CompletedTask;
            });
        _anomalyService
            .AnalyzeTelemetryAsync(telemetry, Arg.Any<Shipment?>(), Arg.Any<CancellationToken>())
            .Returns(new AnomalyAnalysis(false));

        var thrown = await Assert.ThrowsAsync<TransientProcessingException>(() =>
            _handler.Handle(new ProcessTelemetryCommand(telemetry), CancellationToken.None));

        Assert.IsType<InvalidOperationException>(thrown.InnerException);
        await _deduplicationService.Received(1)
            .ReleaseProcessingLockAsync(telemetry.Id, Arg.Any<CancellationToken>());

        var result = await _handler.Handle(new ProcessTelemetryCommand(telemetry), CancellationToken.None);

        Assert.True(result);
        Assert.Equal(2, saveAttempts);
        await _repository.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenCacheFails_StillBroadcasts()
    {
        var telemetry = CreateTelemetry();
        _deduplicationService.TryAcquireProcessingLockAsync(telemetry.Id, Arg.Any<CancellationToken>())
            .Returns(true);
        _anomalyService
            .AnalyzeTelemetryAsync(telemetry, Arg.Any<Shipment?>(), Arg.Any<CancellationToken>())
            .Returns(new AnomalyAnalysis(false));
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
        _anomalyService
            .AnalyzeTelemetryAsync(telemetry, Arg.Any<Shipment?>(), Arg.Any<CancellationToken>())
            .Returns(new AnomalyAnalysis(false));

        await _handler.Handle(new ProcessTelemetryCommand(telemetry), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, telemetry.Id);
        await _deduplicationService.Received(1)
            .TryAcquireProcessingLockAsync(telemetry.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenShipmentLookupFails_StillProcessesTelemetry()
    {
        var telemetry = CreateTelemetry();
        _deduplicationService.TryAcquireProcessingLockAsync(telemetry.Id, Arg.Any<CancellationToken>())
            .Returns(true);
        _shipmentRepository
            .GetActiveShipmentForVehicleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Shipment?>(_ => throw new InvalidOperationException("db timeout"));
        _anomalyService
            .AnalyzeTelemetryAsync(telemetry, Arg.Any<Shipment?>(), Arg.Any<CancellationToken>())
            .Returns(new AnomalyAnalysis(false));

        var result = await _handler.Handle(new ProcessTelemetryCommand(telemetry), CancellationToken.None);

        Assert.True(result);
        await _repository.Received(1).AddAsync(telemetry, Arg.Any<CancellationToken>());
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

    private static Shipment CreateShipment() => new()
    {
        Id = new Guid("a1000000-0000-0000-0000-000000000001"),
        VehicleId = "Truck-001",
        ProductName = "Insulin Glargine",
        BatchNumber = "B-4471",
        MinSafeTempCelsius = 50.0,
        MaxSafeTempCelsius = 100.0,
        ValueAtRiskUsd = 12_400m,
        DepartedAtUtc = new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc),
        ExpectedDeliveryUtc = new DateTime(2026, 7, 7, 18, 0, 0, DateTimeKind.Utc),
        Status = OmniOps.Core.Enums.ShipmentStatus.InTransit
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
    private static GetFleetTelemetryQueryHandler BuildHandler(
        ITelemetryCacheService cache,
        IFleetVehicleRegistry registry,
        IShipmentRepository? shipmentRepo = null)
    {
        if (shipmentRepo is null)
        {
            var defaultRepo = Substitute.For<IShipmentRepository>();
            defaultRepo.GetActiveShipmentForVehicleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns((Shipment?)null);
            return new GetFleetTelemetryQueryHandler(cache, registry, defaultRepo);
        }

        return new GetFleetTelemetryQueryHandler(cache, registry, shipmentRepo);
    }

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

        var handler = BuildHandler(cache, registry);
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

        var handler = BuildHandler(cache, registry);
        var result = await handler.Handle(new GetFleetTelemetryQuery(), CancellationToken.None);

        Assert.Equal(1, result.Summary.WarningCount);
    }

    [Fact]
    public async Task Handle_WhenShipmentPresent_PopulatesShipmentInfoOnTelemetryDto()
    {
        var cache = Substitute.For<ITelemetryCacheService>();
        var registry = Substitute.For<IFleetVehicleRegistry>();
        var shipmentRepo = Substitute.For<IShipmentRepository>();
        registry.GetConfiguredVehicleIds().Returns(["Truck-001"]);

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
        shipmentRepo
            .GetActiveShipmentForVehicleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new Shipment
            {
                Id = Guid.NewGuid(),
                VehicleId = "Truck-001",
                ProductName = "Insulin Glargine",
                BatchNumber = "B-4471",
                MinSafeTempCelsius = 50,
                MaxSafeTempCelsius = 100,
                ValueAtRiskUsd = 12_400m,
                Status = OmniOps.Core.Enums.ShipmentStatus.InTransit
            });

        var handler = BuildHandler(cache, registry, shipmentRepo);
        var result = await handler.Handle(new GetFleetTelemetryQuery(), CancellationToken.None);

        Assert.NotNull(result.Vehicles[0].Shipment);
        Assert.Equal("Insulin Glargine", result.Vehicles[0].Shipment!.ProductName);
        Assert.Equal("B-4471", result.Vehicles[0].Shipment!.BatchNumber);
    }

    [Fact]
    public async Task Handle_WhenShipmentLookupFails_StillReturnsVehicle()
    {
        var cache = Substitute.For<ITelemetryCacheService>();
        var registry = Substitute.For<IFleetVehicleRegistry>();
        var shipmentRepo = Substitute.For<IShipmentRepository>();
        registry.GetConfiguredVehicleIds().Returns(["Truck-001"]);

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
        shipmentRepo
            .GetActiveShipmentForVehicleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Shipment?>(_ => throw new InvalidOperationException("db timeout"));

        var handler = new GetFleetTelemetryQueryHandler(cache, registry, shipmentRepo);
        var result = await handler.Handle(new GetFleetTelemetryQuery(), CancellationToken.None);

        Assert.Single(result.Vehicles);
        Assert.Null(result.Vehicles[0].Shipment);
    }
}
