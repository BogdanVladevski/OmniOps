using System.Net.Http.Json;
using System.Text.Json;

namespace OmniOps.Api.Tests.Integration;

[Collection(OmniOpsApiCollection.Name)]
public class FleetApiIntegrationTests(OmniOpsApiFixture fixture)
{
    private static readonly Guid DefaultFleetId = Guid.Parse("f1000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task GetFleets_ReturnsSeededFleet()
    {
        var fleets = await fixture.Client.GetFromJsonAsync<JsonElement[]>("/api/fleets");
        Assert.NotNull(fleets);
        Assert.NotEmpty(fleets);
        Assert.Contains(fleets, f => f.GetProperty("id").GetGuid() == DefaultFleetId);
    }

    [Fact]
    public async Task GetFleetStatistics_ReturnsMetrics()
    {
        var stats = await fixture.Client.GetFromJsonAsync<JsonElement>(
            $"/api/fleets/{DefaultFleetId}/statistics");
        Assert.True(stats.GetProperty("vehicleCount").GetInt32() >= 0);
        Assert.True(stats.TryGetProperty("driverCount", out _));
    }

    [Fact]
    public async Task GetFleetVehicles_ReturnsSeededVehicles()
    {
        var vehicles = await fixture.Client.GetFromJsonAsync<JsonElement[]>(
            $"/api/fleets/{DefaultFleetId}/vehicles");
        Assert.NotNull(vehicles);
        Assert.NotEmpty(vehicles);
    }

    [Fact]
    public async Task GetGeofences_ReturnsList()
    {
        var geofences = await fixture.Client.GetFromJsonAsync<JsonElement[]>(
            $"/api/geofences?fleetId={DefaultFleetId}");
        Assert.NotNull(geofences);
    }
}
