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
    private readonly IShipmentRepository _shipmentRepository = Substitute.For<IShipmentRepository>();
    private readonly IGeofenceDetectionService _geofenceDetectionService = Substitute.For<IGeofenceDetectionService>();
    private readonly IIncidentDetectionService _incidentDetectionService = Substitute.For<IIncidentDetectionService>();
    private readonly IIncidentRepository _incidentRepository = Substitute.For<IIncidentRepository>();
    private readonly IVehicleRepository _vehicleRepository = Substitute.For<IVehicleRepository>();
    private static readonly VehicleTenant TenantA = new(
        new Guid("0a000000-0000-0000-0000-00000000000a"), new Guid("fa000000-0000-0000-0000-00000000000a"));
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
            _shipmentRepository,
            _geofenceDetectionService,
            _incidentDetectionService,
            _incidentRepository,
            _vehicleRepository,
            _metrics,
            NullLogger<ProcessTelemetryCommandHandler>.Instance);

        _geofenceDetectionService
            .DetectBreachesAsync(Arg.Any<VehicleTelemetry>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Core.Events.GeofenceBreachedEvent>());
        _incidentDetectionService
            .DetectAsync(Arg.Any<VehicleTelemetry>(), Arg.Any<VehicleTenant>(), Arg.Any<CancellationToken>())
            .Returns(IncidentDetectionResult.Empty);

        // Default: every vehicle is registered to exactly one fleet (TenantA).
        _vehicleRepository
            .FindTenantsByExternalIdAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { TenantA });

        // Default: no active shipment for any vehicle.
        _shipmentRepository
            .GetActiveShipmentForVehicleAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
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
            .BroadcastTelemetryUpdateAsync(Arg.Any<Guid>(), Arg.Any<VehicleTelemetry>(), Arg.Any<CancellationToken>());
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
            .BroadcastTelemetryUpdateAsync(Arg.Any<Guid>(), telemetry, Arg.Any<CancellationToken>());
        await _broadcastService.DidNotReceive().BroadcastAlertAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAnomalyDetected_BroadcastsCargoTemperatureAlert()
    {
        var telemetry = CreateTelemetry();
        _deduplicationService.TryAcquireProcessingLockAsync(telemetry.Id, Arg.Any<CancellationToken>())
            .Returns(true);
        _anomalyService
            .AnalyzeTelemetryAsync(telemetry, Arg.Any<Shipment?>(), Arg.Any<CancellationToken>())
            .Returns(new AnomalyAnalysis(true, ExcursionDurationSeconds: 45));

        await _handler.Handle(new ProcessTelemetryCommand(telemetry), CancellationToken.None);

        await _broadcastService.Received(1).BroadcastAlertAsync(
            Arg.Any<Guid>(),
            telemetry.VehicleId,
            CargoTemperatureAlerts.Trend,
            Arg.Any<string>(),
            Arg.Is<string>(m => m.Contains(telemetry.VehicleId)),
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
            .GetActiveShipmentForVehicleAsync(TenantA.FleetId, telemetry.VehicleId, Arg.Any<CancellationToken>())
            .Returns(shipment);
        _anomalyService
            .AnalyzeTelemetryAsync(telemetry, Arg.Any<Shipment?>(), Arg.Any<CancellationToken>())
            .Returns(new AnomalyAnalysis(true, ExcursionDurationSeconds: 90));

        await _handler.Handle(new ProcessTelemetryCommand(telemetry), CancellationToken.None);

        await _broadcastService.Received(1).BroadcastAlertAsync(
            Arg.Any<Guid>(),
            telemetry.VehicleId,
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Is<string>(m => m.Contains(shipment.ProductName) && m.Contains(shipment.BatchNumber)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FirstOutOfRangeReading_IsDescribedAsOutsideRangeNotTrending()
    {
        // Regression: the summary used excursion duration to pick its wording, so the first
        // out-of-range reading (duration 0s) was described as "trending toward excursion".
        var telemetry = CreateTelemetry();
        var shipment = CreateShipment();
        telemetry.EngineTemperature = shipment.MaxSafeTempCelsius + 3;
        _deduplicationService.TryAcquireProcessingLockAsync(telemetry.Id, Arg.Any<CancellationToken>())
            .Returns(true);
        _shipmentRepository
            .GetActiveShipmentForVehicleAsync(TenantA.FleetId, telemetry.VehicleId, Arg.Any<CancellationToken>())
            .Returns(shipment);
        _anomalyService
            .AnalyzeTelemetryAsync(telemetry, Arg.Any<Shipment?>(), Arg.Any<CancellationToken>())
            .Returns(new AnomalyAnalysis(true, OmniOps.Core.Enums.AnomalySeverity.Warning, 0));

        await _handler.Handle(new ProcessTelemetryCommand(telemetry), CancellationToken.None);

        await _broadcastService.Received(1).BroadcastAlertAsync(
            Arg.Any<Guid>(),
            telemetry.VehicleId,
            CargoTemperatureAlerts.Excursion,
            Arg.Any<string>(),
            Arg.Is<string>(m => m.Contains("outside safe range") && !m.Contains("trending")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenCargoOutsideSafeRange_BroadcastsExcursionAlert()
    {
        var telemetry = CreateTelemetry();
        var shipment = CreateShipment();
        telemetry.EngineTemperature = shipment.MaxSafeTempCelsius + 5;
        _deduplicationService.TryAcquireProcessingLockAsync(telemetry.Id, Arg.Any<CancellationToken>())
            .Returns(true);
        _shipmentRepository
            .GetActiveShipmentForVehicleAsync(TenantA.FleetId, telemetry.VehicleId, Arg.Any<CancellationToken>())
            .Returns(shipment);
        _anomalyService
            .AnalyzeTelemetryAsync(telemetry, Arg.Any<Shipment?>(), Arg.Any<CancellationToken>())
            .Returns(new AnomalyAnalysis(true, OmniOps.Core.Enums.AnomalySeverity.Critical, 120));

        await _handler.Handle(new ProcessTelemetryCommand(telemetry), CancellationToken.None);

        await _broadcastService.Received(1).BroadcastAlertAsync(
            Arg.Any<Guid>(),
            telemetry.VehicleId,
            CargoTemperatureAlerts.Excursion,
            Arg.Is<string>(t => t.Contains("out of range")),
            Arg.Is<string>(m => m.Contains("[CRITICAL]")),
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
            .BroadcastTelemetryUpdateAsync(Arg.Any<Guid>(), Arg.Any<VehicleTelemetry>(), Arg.Any<CancellationToken>());
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
    public async Task Handle_WhenPersistenceFails_DoesNotCommitIncidentDetectionState()
    {
        var telemetry = CreateTelemetry();
        var commits = 0;
        _deduplicationService.TryAcquireProcessingLockAsync(telemetry.Id, Arg.Any<CancellationToken>())
            .Returns(true);
        _incidentDetectionService
            .DetectAsync(Arg.Any<VehicleTelemetry>(), Arg.Any<VehicleTenant>(), Arg.Any<CancellationToken>())
            .Returns(new IncidentDetectionResult([], _ => { commits++; return Task.CompletedTask; }));
        _repository.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("db error"));

        await Assert.ThrowsAsync<TransientProcessingException>(() =>
            _handler.Handle(new ProcessTelemetryCommand(telemetry), CancellationToken.None));

        Assert.Equal(0, commits);
    }

    [Fact]
    public async Task Handle_WhenPersistenceSucceeds_CommitsIncidentDetectionState()
    {
        var telemetry = CreateTelemetry();
        var commits = 0;
        _deduplicationService.TryAcquireProcessingLockAsync(telemetry.Id, Arg.Any<CancellationToken>())
            .Returns(true);
        _incidentDetectionService
            .DetectAsync(Arg.Any<VehicleTelemetry>(), Arg.Any<VehicleTenant>(), Arg.Any<CancellationToken>())
            .Returns(new IncidentDetectionResult([], _ => { commits++; return Task.CompletedTask; }));
        _anomalyService
            .AnalyzeTelemetryAsync(telemetry, Arg.Any<Shipment?>(), Arg.Any<CancellationToken>())
            .Returns(new AnomalyAnalysis(false));

        await _handler.Handle(new ProcessTelemetryCommand(telemetry), CancellationToken.None);

        Assert.Equal(1, commits);
    }

    [Fact]
    public async Task Handle_WhenVehicleRegistered_AttributesAllIncidentsToItsTenant()
    {
        var telemetry = CreateTelemetry();
        _deduplicationService.TryAcquireProcessingLockAsync(telemetry.Id, Arg.Any<CancellationToken>())
            .Returns(true);
        _geofenceDetectionService
            .DetectBreachesAsync(Arg.Any<VehicleTelemetry>(), Arg.Any<CancellationToken>())
            .Returns(new[] { new Core.Events.GeofenceBreachedEvent(Guid.NewGuid(), telemetry.VehicleId, Core.Enums.GeofenceBreachType.Exit) });
        _anomalyService
            .AnalyzeTelemetryAsync(telemetry, Arg.Any<Shipment?>(), Arg.Any<CancellationToken>())
            .Returns(new AnomalyAnalysis(false));

        await _handler.Handle(new ProcessTelemetryCommand(telemetry), CancellationToken.None);

        await _incidentDetectionService.Received(1)
            .DetectAsync(telemetry, TenantA, Arg.Any<CancellationToken>());
        await _incidentRepository.Received(1).AddAsync(
            Arg.Is<Incident>(i => i.Type == Core.Enums.IncidentType.GeofenceBreach
                && i.OrganizationId == TenantA.OrganizationId
                && i.FleetId == TenantA.FleetId),
            Arg.Any<CancellationToken>());
        _metrics.DidNotReceive().RecordTenantUnresolved(Arg.Any<string>());
    }

    [Theory]
    [InlineData(0, "not_registered")]
    [InlineData(2, "ambiguous")]
    public async Task Handle_WhenVehicleNotAttributable_UsesUnattributedTenantAndRecordsMetric(int matches, string reason)
    {
        var telemetry = CreateTelemetry();
        _deduplicationService.TryAcquireProcessingLockAsync(telemetry.Id, Arg.Any<CancellationToken>())
            .Returns(true);
        _vehicleRepository
            .FindTenantsByExternalIdAsync(telemetry.VehicleId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Enumerable.Repeat(TenantA, matches).ToArray());
        _anomalyService
            .AnalyzeTelemetryAsync(telemetry, Arg.Any<Shipment?>(), Arg.Any<CancellationToken>())
            .Returns(new AnomalyAnalysis(false));

        await _handler.Handle(new ProcessTelemetryCommand(telemetry), CancellationToken.None);

        await _incidentDetectionService.Received(1)
            .DetectAsync(telemetry, VehicleTenant.Unattributed, Arg.Any<CancellationToken>());
        _metrics.Received(1).RecordTenantUnresolved(reason);
    }

    [Fact]
    public async Task Handle_WhenVehicleQuarantined_DoesNotUseAnyTenantsShipment()
    {
        var telemetry = CreateTelemetry();
        _deduplicationService.TryAcquireProcessingLockAsync(telemetry.Id, Arg.Any<CancellationToken>())
            .Returns(true);
        _vehicleRepository
            .FindTenantsByExternalIdAsync(telemetry.VehicleId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<VehicleTenant>());
        _anomalyService
            .AnalyzeTelemetryAsync(telemetry, Arg.Any<Shipment?>(), Arg.Any<CancellationToken>())
            .Returns(new AnomalyAnalysis(false));

        await _handler.Handle(new ProcessTelemetryCommand(telemetry), CancellationToken.None);

        await _shipmentRepository.DidNotReceive().GetActiveShipmentForVehicleAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _anomalyService.Received(1).AnalyzeTelemetryAsync(telemetry, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenTenantLookupFails_StillDetectsAndPersists()
    {
        var telemetry = CreateTelemetry();
        _deduplicationService.TryAcquireProcessingLockAsync(telemetry.Id, Arg.Any<CancellationToken>())
            .Returns(true);
        _vehicleRepository
            .FindTenantsByExternalIdAsync(telemetry.VehicleId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<VehicleTenant>>(_ => throw new InvalidOperationException("db timeout"));
        _anomalyService
            .AnalyzeTelemetryAsync(telemetry, Arg.Any<Shipment?>(), Arg.Any<CancellationToken>())
            .Returns(new AnomalyAnalysis(false));

        var result = await _handler.Handle(new ProcessTelemetryCommand(telemetry), CancellationToken.None);

        Assert.True(result);
        await _incidentDetectionService.Received(1)
            .DetectAsync(telemetry, VehicleTenant.Unattributed, Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _metrics.Received(1).RecordTenantUnresolved("lookup_failed");
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
            .BroadcastTelemetryUpdateAsync(Arg.Any<Guid>(), telemetry, Arg.Any<CancellationToken>());
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
            .GetActiveShipmentForVehicleAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
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
    private static readonly Guid Fleet = new("fd000000-0000-0000-0000-0000000000aa");

    private static VehicleTelemetry Packet() => new()
    {
        Id = Guid.NewGuid(), VehicleId = "Truck-001", FleetId = Fleet, Latitude = 41.99, Longitude = 21.43,
        Speed = 80, FuelLevel = 75, EngineTemperature = 90, Timestamp = DateTime.UtcNow,
    };

    [Fact]
    public async Task Handle_ReadsTheLatestPacketOfTheCallersOwnVehicle()
    {
        var cache = Substitute.For<ITelemetryCacheService>();
        var scope = Substitute.For<ITenantScope>();
        scope.FleetOfVehicleAsync("Truck-001", Arg.Any<CancellationToken>()).Returns(Fleet);
        var entity = Packet();
        cache.GetLatestTelemetryAsync(Fleet, "Truck-001").Returns(entity);

        var result = await new GetLatestTelemetryQueryHandler(cache, scope)
            .Handle(new GetLatestTelemetryQuery("Truck-001"), CancellationToken.None);

        Assert.Equal(entity.Latitude, result!.Latitude);
    }

    [Fact]
    public async Task Handle_ForAnotherOrganizationsVehicle_ReturnsNothing()
    {
        var cache = Substitute.For<ITelemetryCacheService>();
        var scope = Substitute.For<ITenantScope>();
        scope.FleetOfVehicleAsync("Truck-001", Arg.Any<CancellationToken>())
            .Returns<Guid>(_ => throw new KeyNotFoundException());

        var result = await new GetLatestTelemetryQueryHandler(cache, scope)
            .Handle(new GetLatestTelemetryQuery("Truck-001"), CancellationToken.None);

        Assert.Null(result);
        await cache.DidNotReceive().GetLatestTelemetryAsync(Arg.Any<Guid>(), Arg.Any<string>());
    }
}

public class GetFleetTelemetryQueryHandlerTests
{
    private static readonly Guid CallerOrgId = new("0d000000-0000-0000-0000-00000000000d");
    private static readonly Guid CallerFleetId = new("fd000000-0000-0000-0000-00000000000d");

    private static ITenantContext CallerTenant()
    {
        var tenant = Substitute.For<ITenantContext>();
        tenant.OrganizationId.Returns(CallerOrgId);
        return tenant;
    }

    /// <summary>The caller's organization owns exactly these trucks.</summary>
    private static IJobRepository Owns(params string[] externalIds)
    {
        var jobs = Substitute.For<IJobRepository>();
        jobs.ListVehiclesAsync(CallerOrgId, Arg.Any<CancellationToken>())
            .Returns(externalIds.Select(id => new Vehicle { Id = Guid.NewGuid(), FleetId = CallerFleetId, ExternalId = id }).ToList());
        return jobs;
    }

    private static VehicleTelemetry Packet(string id, double fuel = 80, double temp = 90) => new()
    {
        VehicleId = id, FleetId = CallerFleetId, FuelLevel = fuel, EngineTemperature = temp,
        Latitude = 41.99, Longitude = 21.43, Speed = 70, Timestamp = DateTime.UtcNow,
    };

    private static GetFleetTelemetryQueryHandler Handler(ITelemetryCacheService cache, IJobRepository jobs, IShipmentRepository? shipments = null) =>
        new(cache, jobs, shipments ?? Substitute.For<IShipmentRepository>(), Substitute.For<IVehicleRepository>(), CallerTenant());

    [Fact]
    public async Task Handle_ListsOnlyTheCallersOwnTrucks()
    {
        // Regression: the snapshot used a server-wide vehicle list and showed other companies' trucks.
        var cache = Substitute.For<ITelemetryCacheService>();
        cache.GetLatestTelemetryAsync(Arg.Any<Guid>(), Arg.Any<string>()).Returns(c => Packet(c.ArgAt<string>(1)));

        var result = await Handler(cache, Owns("Truck-001")).Handle(new GetFleetTelemetryQuery(), CancellationToken.None);

        Assert.Equal("Truck-001", Assert.Single(result.Vehicles).VehicleId);
        await cache.Received(1).GetLatestTelemetryAsync(CallerFleetId, "Truck-001");
    }

    [Fact]
    public async Task Handle_ReturnsActiveVehiclesAndSummary()
    {
        var cache = Substitute.For<ITelemetryCacheService>();
        cache.GetLatestTelemetryAsync(CallerFleetId, "Truck-001").Returns(Packet("Truck-001"));
        cache.GetLatestTelemetryAsync(CallerFleetId, "Truck-002").Returns((VehicleTelemetry?)null);

        var result = await Handler(cache, Owns("Truck-001", "Truck-002")).Handle(new GetFleetTelemetryQuery(), CancellationToken.None);

        Assert.Equal(2, result.Summary.ConfiguredVehicleCount);
        Assert.Equal(1, result.Summary.ActiveVehicleCount);
        Assert.Equal(80, result.Summary.AverageFuelLevel);
    }

    [Fact]
    public async Task Handle_CountsVehiclesInWarningState()
    {
        var cache = Substitute.For<ITelemetryCacheService>();
        cache.GetLatestTelemetryAsync(CallerFleetId, "Truck-001").Returns(Packet("Truck-001", fuel: 20, temp: 95));

        var result = await Handler(cache, Owns("Truck-001")).Handle(new GetFleetTelemetryQuery(), CancellationToken.None);

        Assert.Equal(1, result.Summary.WarningCount);
    }

    [Fact]
    public async Task Handle_WhenShipmentPresent_PopulatesShipmentInfo()
    {
        var cache = Substitute.For<ITelemetryCacheService>();
        cache.GetLatestTelemetryAsync(CallerFleetId, "Truck-001").Returns(Packet("Truck-001"));
        var shipments = Substitute.For<IShipmentRepository>();
        shipments.GetActiveShipmentForVehicleAsync(CallerFleetId, "Truck-001", Arg.Any<CancellationToken>())
            .Returns(new Shipment
            {
                Id = Guid.NewGuid(), VehicleId = "Truck-001", ProductName = "Insulin Glargine", BatchNumber = "B-4471",
                MinSafeTempCelsius = 2, MaxSafeTempCelsius = 8, ValueAtRiskUsd = 12_400m,
                Status = OmniOps.Core.Enums.ShipmentStatus.InTransit,
            });

        var result = await Handler(cache, Owns("Truck-001"), shipments).Handle(new GetFleetTelemetryQuery(), CancellationToken.None);

        Assert.Equal("Insulin Glargine", result.Vehicles[0].Shipment!.ProductName);
    }

    [Fact]
    public async Task Handle_WhenShipmentLookupFails_StillReturnsVehicle()
    {
        var cache = Substitute.For<ITelemetryCacheService>();
        cache.GetLatestTelemetryAsync(CallerFleetId, "Truck-001").Returns(Packet("Truck-001"));
        var shipments = Substitute.For<IShipmentRepository>();
        shipments.GetActiveShipmentForVehicleAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Shipment?>(_ => throw new InvalidOperationException("db timeout"));

        var result = await Handler(cache, Owns("Truck-001"), shipments).Handle(new GetFleetTelemetryQuery(), CancellationToken.None);

        Assert.Null(Assert.Single(result.Vehicles).Shipment);
    }
}
