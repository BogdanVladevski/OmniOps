using Microsoft.EntityFrameworkCore;
using OmniOps.Core.Entities;
using OmniOps.Core.Enums;
using OmniOps.Infrastructure.Data;
using OmniOps.Infrastructure.Services;

namespace OmniOps.Infrastructure.Tests.Repositories;

public class ShipmentRepositoryTenantTests
{
    private static readonly Guid OtherOrgId = new("0c000000-0000-0000-0000-00000000000c");
    private static readonly Guid OtherFleetId = new("fc000000-0000-0000-0000-00000000000c");
    private static readonly Guid OtherShipmentId = new("ac000000-0000-0000-0000-00000000000c");

    /// <summary>Seed data (default fleet, Truck-001 + shipment) plus another tenant's Truck-001 with its own shipment.</summary>
    private static async Task<AppDbContext> CreateContextWithSharedVehicleIdAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"shipment-tenant-{Guid.NewGuid()}")
            .Options;
        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        context.Fleets.Add(new Fleet { Id = OtherFleetId, OrganizationId = OtherOrgId, Name = "Other tenant fleet" });
        context.Vehicles.Add(new Vehicle { Id = Guid.NewGuid(), FleetId = OtherFleetId, ExternalId = "Truck-001" });
        context.Shipments.Add(new Shipment
        {
            Id = OtherShipmentId,
            FleetId = OtherFleetId,
            VehicleId = "Truck-001",
            ProductName = "Other tenant product",
            BatchNumber = "X-1",
            MinSafeTempCelsius = 2,
            MaxSafeTempCelsius = 8,
            Status = ShipmentStatus.InTransit
        });
        await context.SaveChangesAsync();
        return context;
    }

    [Fact]
    public async Task GetActiveShipmentForVehicleAsync_SameVehicleIdInTwoFleets_ReturnsOnlyRequestedFleetsShipment()
    {
        await using var context = await CreateContextWithSharedVehicleIdAsync();
        var repository = new ShipmentRepository(context);

        var defaultFleets = await repository.GetActiveShipmentForVehicleAsync(TenantSeed.DefaultFleetId, "Truck-001");
        var otherFleets = await repository.GetActiveShipmentForVehicleAsync(OtherFleetId, "Truck-001");

        Assert.NotNull(defaultFleets);
        Assert.NotEqual(OtherShipmentId, defaultFleets!.Id);
        Assert.Equal(OtherShipmentId, otherFleets?.Id);
    }

    [Fact]
    public async Task GetByIdForOrganizationAsync_OtherOrganizationsShipment_ReturnsNull()
    {
        await using var context = await CreateContextWithSharedVehicleIdAsync();
        var repository = new ShipmentRepository(context);

        Assert.Equal(OtherShipmentId,
            (await repository.GetByIdForOrganizationAsync(OtherShipmentId, OtherOrgId))?.Id);
        Assert.Null(await repository.GetByIdForOrganizationAsync(OtherShipmentId, TenantSeed.DefaultOrganizationId));
    }

    [Fact]
    public async Task FindFleetIdInOrganizationAsync_ReturnsFleetWithinCallersOrganizationOnly()
    {
        await using var context = await CreateContextWithSharedVehicleIdAsync();
        var vehicles = new VehicleRepository(context);

        Assert.Equal(TenantSeed.DefaultFleetId,
            await vehicles.FindFleetIdInOrganizationAsync("Truck-001", TenantSeed.DefaultOrganizationId));
        Assert.Equal(OtherFleetId,
            await vehicles.FindFleetIdInOrganizationAsync("Truck-001", OtherOrgId));
        Assert.Null(await vehicles.FindFleetIdInOrganizationAsync("Truck-002", OtherOrgId));
    }
}
