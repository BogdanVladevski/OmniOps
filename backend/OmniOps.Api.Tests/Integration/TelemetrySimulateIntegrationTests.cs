using System.Net.Http.Json;
using System.Text.Json;

namespace OmniOps.Api.Tests.Integration;

[Collection(OmniOpsApiCollection.Name)]
public class TelemetrySimulateIntegrationTests(OmniOpsApiFixture fixture)
{
    [Fact]
    public async Task SimulateTelemetry_QueuesPacketsToKafka()
    {
        var response = await fixture.Client.PostAsync(
            "/api/test/simulate/Truck-001?packets=3",
            content: null);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, body.GetProperty("packetsSent").GetInt32());
        Assert.Equal("Truck-001", body.GetProperty("vehicleId").GetString());
    }

    [Fact]
    public async Task GetFleetTelemetry_ReturnsVehicleList()
    {
        var snapshot = await fixture.Client.GetFromJsonAsync<JsonElement>("/api/telemetry/fleet");
        Assert.True(snapshot.TryGetProperty("vehicles", out var vehicles));
        Assert.Equal(JsonValueKind.Array, vehicles.ValueKind);
    }
}
