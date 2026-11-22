using System.Net.Http.Json;
using System.Text.Json;

namespace OmniOps.Api.Tests.Integration;

[Collection(OmniOpsApiCollection.Name)]
public class AnalyticsPredictionIntegrationTests(OmniOpsApiFixture fixture)
{
    private static readonly Guid DefaultFleetId = Guid.Parse("f1000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task GetFleetAnalytics_ReturnsKpis()
    {
        var to = DateTime.UtcNow;
        var from = to.AddHours(-6);
        var url = $"/api/analytics/fleet/{DefaultFleetId}?fromUtc={from:O}&toUtc={to:O}";
        var analytics = await fixture.Client.GetFromJsonAsync<JsonElement>(url);
        Assert.True(analytics.TryGetProperty("fleetId", out _));
    }

    [Fact]
    public async Task GetOperationalAnalytics_ReturnsCounts()
    {
        var to = DateTime.UtcNow;
        var from = to.AddHours(-6);
        var url = $"/api/analytics/operational/{DefaultFleetId}?fromUtc={from:O}&toUtc={to:O}";
        var ops = await fixture.Client.GetFromJsonAsync<JsonElement>(url);
        Assert.True(ops.TryGetProperty("openIncidents", out _));
    }

    [Fact]
    public async Task GetVehicleHealthPrediction_ReturnsScore()
    {
        var health = await fixture.Client.GetFromJsonAsync<JsonElement>(
            "/api/predictions/vehicles/Truck-001/health");
        Assert.True(health.TryGetProperty("score", out _));
    }
}
