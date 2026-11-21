using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;
using OmniOps.Core.Domain;
using OmniOps.Core.Entities;
using OmniOps.Core.Enums;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Services;

namespace OmniOps.Infrastructure.Tests.Services;

public class IncidentDetectionServiceTests
{
    private readonly IDistributedCache _cache =
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
    private readonly IShipmentRepository _shipments = Substitute.For<IShipmentRepository>();
    private readonly IncidentDetectionService _service;
    private static readonly VehicleTenant Tenant = new(
        new Guid("0b000000-0000-0000-0000-00000000000b"), new Guid("fb000000-0000-0000-0000-00000000000b"));

    public IncidentDetectionServiceTests()
    {
        _shipments.GetActiveShipmentForVehicleAsync("Truck-001", Arg.Any<CancellationToken>())
            .Returns(new Shipment
            {
                Id = Guid.NewGuid(),
                VehicleId = "Truck-001",
                ProductName = "Insulin Glargine",
                BatchNumber = "B-4471",
                MinSafeTempCelsius = 2,
                MaxSafeTempCelsius = 8,
                Status = ShipmentStatus.InTransit
            });
        _service = new IncidentDetectionService(_cache, _shipments);
    }

    [Fact]
    public async Task DetectAsync_WithoutCommit_RetryStillDetectsExcursion()
    {
        var packet = Telemetry(temp: 15, speed: 60);

        var first = await _service.DetectAsync(packet, Tenant);
        // Simulate persistence failure: handler never commits, packet is retried.
        var retry = await _service.DetectAsync(packet, Tenant);

        Assert.Contains(first.Incidents, i => i.Type == IncidentType.TemperatureExcursion);
        Assert.Contains(retry.Incidents, i => i.Type == IncidentType.TemperatureExcursion);
    }

    [Fact]
    public async Task DetectAsync_AfterCommit_SuppressesDuplicateWithinCooldown()
    {
        var first = await _service.DetectAsync(Telemetry(temp: 15, speed: 60), Tenant);
        await first.CommitAsync();

        var next = await _service.DetectAsync(Telemetry(temp: 16, speed: 60, secondsLater: 10), Tenant);

        Assert.DoesNotContain(next.Incidents, i => i.Type == IncidentType.TemperatureExcursion);
    }

    [Fact]
    public async Task DetectAsync_WithoutCommit_RetryStillDetectsHarshBraking()
    {
        var baseline = await _service.DetectAsync(Telemetry(temp: 5, speed: 90), Tenant);
        await baseline.CommitAsync();

        var braking = Telemetry(temp: 5, speed: 40, secondsLater: 5);
        await _service.DetectAsync(braking, Tenant);
        var retry = await _service.DetectAsync(braking, Tenant);

        Assert.Contains(retry.Incidents, i => i.Type == IncidentType.HarshBraking);
    }

    [Fact]
    public async Task DetectAsync_StampsIncidentsWithGivenTenant()
    {
        var result = await _service.DetectAsync(Telemetry(temp: 15, speed: 130), Tenant);

        Assert.NotEmpty(result.Incidents);
        Assert.All(result.Incidents, i =>
        {
            Assert.Equal(Tenant.OrganizationId, i.OrganizationId);
            Assert.Equal(Tenant.FleetId, i.FleetId);
        });
    }

    [Fact]
    public async Task DetectAsync_SameVehicleIdInTwoFleets_CooldownsAreIsolated()
    {
        var otherTenant = new VehicleTenant(Guid.NewGuid(), Guid.NewGuid());
        var first = await _service.DetectAsync(Telemetry(temp: 15, speed: 60), Tenant);
        await first.CommitAsync();

        var other = await _service.DetectAsync(Telemetry(temp: 15, speed: 60, secondsLater: 5), otherTenant);

        Assert.Contains(other.Incidents, i => i.Type == IncidentType.TemperatureExcursion
            && i.FleetId == otherTenant.FleetId);
    }

    private static readonly DateTime T0 = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    private static VehicleTelemetry Telemetry(double temp, double speed, int secondsLater = 0) => new()
    {
        Id = Guid.NewGuid(),
        VehicleId = "Truck-001",
        Latitude = 41.99,
        Longitude = 21.43,
        Speed = speed,
        FuelLevel = 75,
        EngineTemperature = temp,
        Timestamp = T0.AddSeconds(secondsLater)
    };
}
