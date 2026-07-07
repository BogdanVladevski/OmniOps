using System.Net.Http.Json;
using System.Text.Json;

namespace OmniOps.Api.Tests.Integration;

[Collection(OmniOpsApiCollection.Name)]
public class DemoIntegrationTests(OmniOpsApiFixture fixture)
{
    private static readonly Guid DefaultFleetId = Guid.Parse("f1000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task GetDemoStatus_ReturnsSeededOrganization()
    {
        var status = await fixture.Client.GetFromJsonAsync<JsonElement>("/api/v1/demo/status");
        Assert.Equal(DefaultFleetId, status.GetProperty("defaultFleetId").GetGuid());
        Assert.True(status.GetProperty("vehicleCount").GetInt32() > 0);
        Assert.True(status.GetProperty("isDemoOrganization").GetBoolean());
    }

    [Fact]
    public async Task BootstrapDemo_QueuesTelemetryPackets()
    {
        var response = await fixture.Client.PostAsJsonAsync(
            "/api/v1/demo/bootstrap",
            new { packetsPerVehicle = 2 });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("vehiclesSimulated").GetInt32() > 0);
        Assert.True(body.GetProperty("totalPacketsQueued").GetInt32() > 0);
    }
}
