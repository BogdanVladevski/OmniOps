using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OmniOps.Application.Commands;
using OmniOps.Application.Commands.Handlers;
using OmniOps.Application.Queries;
using OmniOps.Application.Queries.Handlers;
using OmniOps.Core.Domain;
using OmniOps.Core.Entities;
using OmniOps.Core.Enums;
using OmniOps.Core.Exceptions;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Tests.Handlers;

public class JobHandlerTests
{
    private static readonly Guid OrgA = new("0a000000-0000-0000-0000-0000000000aa");
    private static readonly Guid FleetA = new("fa000000-0000-0000-0000-0000000000aa");

    private readonly IJobRepository _jobs = Substitute.For<IJobRepository>();
    private readonly ITenantContext _tenant = Substitute.For<ITenantContext>();
    private readonly IAuditService _audit = Substitute.For<IAuditService>();
    private readonly ITelemetryBroadcastService _broadcast = Substitute.For<ITelemetryBroadcastService>();
    private readonly ITelemetryRepository _telemetry = Substitute.For<ITelemetryRepository>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();

    public JobHandlerTests()
    {
        _tenant.OrganizationId.Returns(OrgA);
        _telemetry.GetExistingIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid>());
    }

    private static Trip Job(TripStatus status = TripStatus.InProgress, int photos = 0)
    {
        var vehicle = new Vehicle { Id = Guid.NewGuid(), FleetId = FleetA, ExternalId = "Truck-001" };
        var job = new Trip
        {
            Id = Guid.NewGuid(),
            OrganizationId = OrgA,
            FleetId = FleetA,
            VehicleId = vehicle.Id,
            Vehicle = vehicle,
            Status = status,
            CustomerName = "Pharmacy Centar",
            Destination = "Skopje",
            TrackingToken = TrackingToken.Create(),
            CreatedAtUtc = DateTime.UtcNow.AddHours(-2),
            StartedAtUtc = status == TripStatus.Planned ? null : DateTime.UtcNow.AddHours(-1),
        };
        for (var i = 0; i < photos; i++)
        {
            job.Documents.Add(new TripDocument { Id = Guid.NewGuid(), TripId = job.Id, Kind = TripDocumentKind.DeliveryPhoto });
        }

        return job;
    }

    private void Returns(Trip job) =>
        _jobs.GetAsync(job.Id, OrgA, Arg.Any<CancellationToken>()).Returns(job);

    // ---- Create -----------------------------------------------------------------------------

    [Fact]
    public async Task Create_WithVehicleFromAnotherOrganization_IsNotFound()
    {
        // GetVehicleAsync returns null for vehicles outside the caller's organization.
        var handler = new CreateJobCommandHandler(_jobs, _tenant, _audit);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(
            new CreateJobCommand(Guid.NewGuid(), null, "Customer", null, null, "Skopje", null, null, null),
            CancellationToken.None));
        await _jobs.DidNotReceive().AddAsync(Arg.Any<Trip>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_StampsCallerOrganizationVehicleFleetAndTrackingToken()
    {
        var vehicle = new Vehicle { Id = Guid.NewGuid(), FleetId = FleetA, ExternalId = "Truck-002" };
        _jobs.GetVehicleAsync(vehicle.Id, OrgA, Arg.Any<CancellationToken>()).Returns(vehicle);
        var handler = new CreateJobCommandHandler(_jobs, _tenant, _audit);

        var dto = await handler.Handle(
            new CreateJobCommand(vehicle.Id, null, " Customer ", "PO-7", "Depot", " Bitola ", 41.03, 21.33, null),
            CancellationToken.None);

        await _jobs.Received(1).AddAsync(Arg.Is<Trip>(t =>
            t.OrganizationId == OrgA && t.FleetId == FleetA && t.Status == TripStatus.Planned
            && TrackingToken.LooksValid(t.TrackingToken) && t.CustomerName == "Customer"), Arg.Any<CancellationToken>());
        Assert.Equal("Bitola", dto.Destination);
        Assert.StartsWith("/t/", dto.TrackingPath);
    }

    // ---- Start ------------------------------------------------------------------------------

    [Fact]
    public async Task Start_IsIdempotentForAJobAlreadyOnTheWay()
    {
        var job = Job(TripStatus.InProgress);
        var startedAt = job.StartedAtUtc;
        Returns(job);

        var dto = await new StartJobCommandHandler(_jobs, _tenant, _audit).Handle(new StartJobCommand(job.Id), CancellationToken.None);

        Assert.Equal("InProgress", dto.Status);
        Assert.Equal(startedAt, job.StartedAtUtc);
        await _audit.DidNotReceive().LogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_DeliveredJob_IsRejected()
    {
        var job = Job(TripStatus.Completed);
        Returns(job);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new StartJobCommandHandler(_jobs, _tenant, _audit).Handle(new StartJobCommand(job.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Start_JobOfAnotherOrganization_IsNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new StartJobCommandHandler(_jobs, _tenant, _audit).Handle(new StartJobCommand(Guid.NewGuid()), CancellationToken.None));
    }

    // ---- Locations --------------------------------------------------------------------------

    private RecordJobLocationsCommandHandler LocationsHandler() =>
        new(_jobs, _telemetry, _tenant, _mediator, NullLogger<RecordJobLocationsCommandHandler>.Instance);

    private static JobLocationPoint Point(DateTime at, double lat = 41.99) =>
        new(Guid.NewGuid(), lat, 21.43, 50, 90, 8, at);

    [Fact]
    public async Task Locations_BeforeStart_AreRejected()
    {
        var job = Job(TripStatus.Planned);
        Returns(job);

        await Assert.ThrowsAsync<BusinessRuleException>(() => LocationsHandler().Handle(
            new RecordJobLocationsCommand(job.Id, [Point(DateTime.UtcNow)]), CancellationToken.None));
        await _mediator.DidNotReceive().Send(Arg.Any<ProcessTelemetryCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Locations_UseTheJobsTenantAndCarryNoFuelOrCargoReading()
    {
        var job = Job();
        Returns(job);
        var point = Point(DateTime.UtcNow.AddMinutes(-1));

        await LocationsHandler().Handle(new RecordJobLocationsCommand(job.Id, [point]), CancellationToken.None);

        await _mediator.Received(1).Send(Arg.Is<ProcessTelemetryCommand>(c =>
            c.KnownTenant == new VehicleTenant(OrgA, FleetA)
            && c.Telemetry.Id == point.Id
            && c.Telemetry.VehicleId == "Truck-001"
            && c.Telemetry.EngineTemperature == null
            && c.Telemetry.FuelLevel == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Locations_AlreadyStoredPointsAreSkipped_SoResendingIsSafe()
    {
        var job = Job();
        Returns(job);
        var stored = Point(DateTime.UtcNow.AddMinutes(-2));
        var fresh = Point(DateTime.UtcNow.AddMinutes(-1));
        _telemetry.GetExistingIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid> { stored.Id });

        var result = await LocationsHandler().Handle(new RecordJobLocationsCommand(job.Id, [stored, fresh]), CancellationToken.None);

        Assert.Equal(1, result.Accepted);
        Assert.Equal(1, result.Skipped);
        await _mediator.Received(1).Send(Arg.Is<ProcessTelemetryCommand>(c => c.Telemetry.Id == fresh.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Locations_LastPositionTakesTheNewestPointAndNeverGoesBackwards()
    {
        var job = Job();
        job.LastPositionAtUtc = DateTime.UtcNow.AddMinutes(-1);
        job.LastLatitude = 42.0;
        Returns(job);

        // Older than the stored position: stored, but must not move the truck back on the map.
        await LocationsHandler().Handle(
            new RecordJobLocationsCommand(job.Id, [Point(DateTime.UtcNow.AddMinutes(-5), lat: 41.0)]), CancellationToken.None);
        Assert.Equal(42.0, job.LastLatitude);

        var newest = Point(DateTime.UtcNow, lat: 41.5);
        await LocationsHandler().Handle(
            new RecordJobLocationsCommand(job.Id, [Point(DateTime.UtcNow.AddSeconds(-30), lat: 41.4), newest]), CancellationToken.None);
        Assert.Equal(41.5, job.LastLatitude);
        Assert.Equal(newest.RecordedAtUtc, job.LastPositionAtUtc);
    }

    [Fact]
    public async Task Locations_AfterDelivery_OnlyPointsFromBeforeDeliveryAreKept()
    {
        var job = Job(TripStatus.Completed);
        job.CompletedAtUtc = DateTime.UtcNow.AddMinutes(-30);
        Returns(job);
        var beforeDelivery = Point(job.CompletedAtUtc.Value.AddMinutes(-1));
        var longAfter = Point(DateTime.UtcNow.AddMinutes(-1));

        var result = await LocationsHandler().Handle(
            new RecordJobLocationsCommand(job.Id, [beforeDelivery, longAfter]), CancellationToken.None);

        Assert.Equal(1, result.Accepted);
        await _mediator.Received(1).Send(Arg.Is<ProcessTelemetryCommand>(c => c.Telemetry.Id == beforeDelivery.Id), Arg.Any<CancellationToken>());
    }

    // ---- Photos -----------------------------------------------------------------------------

    [Fact]
    public async Task Photo_IsWrittenToStorageBeforeTheDatabaseRecord()
    {
        var job = Job();
        Returns(job);
        var files = Substitute.For<IFileStore>();
        var calls = new List<string>();
        files.When(f => f.SaveAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())).Do(_ => calls.Add("file"));
        _jobs.When(j => j.SaveChangesAsync(Arg.Any<CancellationToken>())).Do(_ => calls.Add("db"));

        var dto = await new AddJobPhotoCommandHandler(_jobs, files, _tenant).Handle(
            new AddJobPhotoCommand(job.Id, new MemoryStream([1, 2, 3]), "image/jpeg", 3, null, null), CancellationToken.None);

        Assert.Equal(["file", "db"], calls);
        Assert.Equal("DeliveryPhoto", dto.Kind);
        await files.Received(1).SaveAsync(
            Arg.Is<string>(k => k.StartsWith($"{OrgA:N}/{job.Id:N}/") && k.EndsWith(".jpg")), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Photo_OverTheLimit_IsRejected()
    {
        var job = Job(photos: AddJobPhotoCommand.MaxPhotosPerJob);
        Returns(job);

        await Assert.ThrowsAsync<BusinessRuleException>(() => new AddJobPhotoCommandHandler(_jobs, Substitute.For<IFileStore>(), _tenant)
            .Handle(new AddJobPhotoCommand(job.Id, new MemoryStream([1]), "image/jpeg", 1, null, null), CancellationToken.None));
    }

    // ---- Deliver ----------------------------------------------------------------------------

    private DeliverJobCommandHandler DeliverHandler() =>
        new(_jobs, _tenant, _audit, _broadcast, NullLogger<DeliverJobCommandHandler>.Instance);

    [Fact]
    public async Task Deliver_WithoutAPhoto_IsRejected()
    {
        var job = Job(photos: 0);
        Returns(job);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            DeliverHandler().Handle(new DeliverJobCommand(job.Id, "Ana", null, null, null), CancellationToken.None));

        Assert.Contains("photo", ex.Message);
        Assert.Equal(TripStatus.InProgress, job.Status);
    }

    [Fact]
    public async Task Deliver_ClosesTheJob_ThenTellsTheOfficeAfterSaving()
    {
        var job = Job(photos: 1);
        job.Reference = "PO-7";
        Returns(job);
        var calls = new List<string>();
        _audit.When(a => a.LogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<CancellationToken>())).Do(_ => calls.Add("save"));
        _broadcast.When(b => b.BroadcastAlertAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>())).Do(_ => calls.Add("broadcast"));

        var dto = await DeliverHandler().Handle(new DeliverJobCommand(job.Id, " Ana Petrovska ", "Left at door 2", 41.9, 21.4), CancellationToken.None);

        Assert.Equal("Completed", dto.Status);
        Assert.Equal("Ana Petrovska", job.RecipientName);
        Assert.NotNull(job.CompletedAtUtc);
        Assert.Equal(["save", "broadcast"], calls);
        await _broadcast.Received(1).BroadcastAlertAsync("Truck-001", JobAlerts.Delivered, Arg.Any<string>(),
            Arg.Is<string>(m => m.Contains("PO-7") && m.Contains("Ana Petrovska")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deliver_Twice_KeepsTheFirstDelivery()
    {
        var job = Job(TripStatus.Completed, photos: 1);
        var deliveredAt = DateTime.UtcNow.AddMinutes(-10);
        job.CompletedAtUtc = deliveredAt;
        job.RecipientName = "Ana";
        Returns(job);

        await DeliverHandler().Handle(new DeliverJobCommand(job.Id, "Someone else", null, null, null), CancellationToken.None);

        Assert.Equal("Ana", job.RecipientName);
        Assert.Equal(deliveredAt, job.CompletedAtUtc);
        await _broadcast.DidNotReceive().BroadcastAlertAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ---- Public tracking --------------------------------------------------------------------

    private GetPublicTrackingQueryHandler TrackingHandler()
    {
        var tenants = Substitute.For<ITenantRepository>();
        tenants.GetOrganizationAsync(OrgA, Arg.Any<CancellationToken>()).Returns(new Organization { Id = OrgA, Name = "Vardar Transport" });
        return new GetPublicTrackingQueryHandler(_jobs, tenants);
    }

    [Fact]
    public async Task Tracking_OnTheWay_ShowsPositionAndEstimate()
    {
        var job = Job();
        job.LastLatitude = 41.99;
        job.LastLongitude = 21.43;
        job.LastPositionAtUtc = DateTime.UtcNow;
        job.DestinationLatitude = 41.03;
        job.DestinationLongitude = 21.33;
        _jobs.GetByTrackingTokenAsync(job.TrackingToken!, Arg.Any<CancellationToken>()).Returns(job);

        var dto = await TrackingHandler().Handle(new GetPublicTrackingQuery(job.TrackingToken!), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal("OnTheWay", dto!.Status);
        Assert.Equal("Vardar Transport", dto.CompanyName);
        Assert.Equal(41.99, dto.Latitude);
        Assert.NotNull(dto.EtaUtc);
        Assert.InRange(dto.RemainingKm!.Value, 100, 180);
    }

    [Fact]
    public async Task Tracking_AfterDelivery_HidesTheTrucksPosition()
    {
        var job = Job(TripStatus.Completed);
        job.CompletedAtUtc = DateTime.UtcNow.AddHours(-1);
        job.RecipientName = "Ana";
        job.LastLatitude = 41.99;
        job.LastLongitude = 21.43;
        job.LastPositionAtUtc = DateTime.UtcNow.AddHours(-1);
        _jobs.GetByTrackingTokenAsync(job.TrackingToken!, Arg.Any<CancellationToken>()).Returns(job);

        var dto = await TrackingHandler().Handle(new GetPublicTrackingQuery(job.TrackingToken!), CancellationToken.None);

        Assert.Equal("Delivered", dto!.Status);
        Assert.Null(dto.Latitude);
        Assert.Null(dto.PositionAtUtc);
        Assert.Equal("Ana", dto.RecipientName);
    }

    [Fact]
    public async Task Tracking_LinkExpiresAWeekAfterDelivery()
    {
        var job = Job(TripStatus.Completed);
        job.CompletedAtUtc = DateTime.UtcNow - GetPublicTrackingQueryHandler.LinkLifetimeAfterDelivery - TimeSpan.FromMinutes(1);
        _jobs.GetByTrackingTokenAsync(job.TrackingToken!, Arg.Any<CancellationToken>()).Returns(job);

        Assert.Null(await TrackingHandler().Handle(new GetPublicTrackingQuery(job.TrackingToken!), CancellationToken.None));
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("has spaces in it but long enough")]
    [InlineData("../../etc/passwd/xxxxxxxxxxxx")]
    public async Task Tracking_MalformedToken_IsNotLookedUp(string token)
    {
        Assert.Null(await TrackingHandler().Handle(new GetPublicTrackingQuery(token), CancellationToken.None));
        await _jobs.DidNotReceive().GetByTrackingTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ---- ETA --------------------------------------------------------------------------------

    [Fact]
    public void Eta_SkopjeToBitola_IsAboutTwoAndAHalfHoursAtTruckSpeed()
    {
        var at = new DateTime(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);
        var (km, eta) = DeliveryEta.Estimate(41.9981, 21.4254, 41.0297, 21.3292, at);

        // ~108 km straight line × 1.3 road factor ≈ 140 km; at 60 km/h ≈ 2 h 20 min.
        Assert.InRange(km, 130, 150);
        Assert.InRange((eta - at).TotalMinutes, 130, 150);
    }

    [Fact]
    public void Eta_AtTheDoor_IsNow()
    {
        var at = DateTime.UtcNow;
        var (km, eta) = DeliveryEta.Estimate(41.9981, 21.4254, 41.9990, 21.4260, at);

        Assert.Equal(0, km);
        Assert.Equal(at, eta);
    }
}
