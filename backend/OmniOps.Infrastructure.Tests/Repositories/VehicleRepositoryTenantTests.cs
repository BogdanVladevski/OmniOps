using Microsoft.EntityFrameworkCore;
using OmniOps.Core.Domain;
using OmniOps.Core.Entities;
using OmniOps.Infrastructure.Data;
using OmniOps.Infrastructure.Services;

namespace OmniOps.Infrastructure.Tests.Repositories;

public class VehicleRepositoryTenantTests
{
    private static readonly Guid OtherOrgId = new("0c000000-0000-0000-0000-00000000000c");
    private static readonly Guid OtherFleetId = new("fc000000-0000-0000-0000-00000000000c");

    private static async Task<AppDbContext> CreateSeededContextAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"vehicle-tenant-{Guid.NewGuid()}")
            .Options;
        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync(); // applies HasData seed: Truck-001..003 in the default fleet
        return context;
    }

    [Fact]
    public async Task FindTenantsByExternalIdAsync_SeededVehicle_ReturnsItsFleetAndOrganization()
    {
        await using var context = await CreateSeededContextAsync();
        var repository = new VehicleRepository(context);

        var tenants = await repository.FindTenantsByExternalIdAsync("Truck-001", max: 2);

        var tenant = Assert.Single(tenants);
        Assert.Equal(new VehicleTenant(TenantSeed.DefaultOrganizationId, TenantSeed.DefaultFleetId), tenant);
    }

    [Fact]
    public async Task FindTenantsByExternalIdAsync_SameExternalIdInTwoFleets_ReturnsBoth()
    {
        await using var context = await CreateSeededContextAsync();
        context.Fleets.Add(new Fleet { Id = OtherFleetId, OrganizationId = OtherOrgId, Name = "Other tenant fleet" });
        context.Vehicles.Add(new Vehicle { Id = Guid.NewGuid(), FleetId = OtherFleetId, ExternalId = "Truck-001" });
        await context.SaveChangesAsync();
        var repository = new VehicleRepository(context);

        var tenants = await repository.FindTenantsByExternalIdAsync("Truck-001", max: 2);

        Assert.Equal(2, tenants.Count);
        Assert.Contains(tenants, t => t.OrganizationId == OtherOrgId && t.FleetId == OtherFleetId);
    }

    [Fact]
    public async Task FindTenantsByExternalIdAsync_UnknownVehicle_ReturnsEmpty()
    {
        await using var context = await CreateSeededContextAsync();
        var repository = new VehicleRepository(context);

        Assert.Empty(await repository.FindTenantsByExternalIdAsync("Truck-999", max: 2));
    }
}
