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
    public async Task BootstrapDemo_LoadsADeliveryDay_AndReRunningMovesTrucksInsteadOfDuplicating()
    {
        var first = await fixture.Client.PostAsJsonAsync("/api/v1/demo/bootstrap", new { });
        first.EnsureSuccessStatusCode();
        var second = await fixture.Client.PostAsJsonAsync("/api/v1/demo/bootstrap", new { });
        second.EnsureSuccessStatusCode();
        Assert.Equal(0, (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("jobsCreated").GetInt32());

        var jobs = await fixture.Client.GetFromJsonAsync<JsonElement[]>("/api/v1/jobs?scope=active");
        var demo = jobs!.Where(j => j.GetProperty("reference").GetString()?.StartsWith("DEMO-") == true).ToList();

        Assert.Equal(3, demo.Count);
        var onTheWay = demo.Where(j => j.GetProperty("status").GetString() == "InProgress").ToList();
        Assert.Equal(2, onTheWay.Count);
        Assert.All(onTheWay, j =>
        {
            Assert.Equal(JsonValueKind.Number, j.GetProperty("lastLatitude").ValueKind);
            Assert.Equal(JsonValueKind.String, j.GetProperty("etaUtc").ValueKind);
            Assert.True(j.GetProperty("remainingKm").GetDouble() > 0);
            // The trail ends at "now", so the office does not see a stale truck.
            Assert.True(DateTime.UtcNow - j.GetProperty("lastPositionAtUtc").GetDateTime().ToUniversalTime() < TimeSpan.FromMinutes(2));
        });
        Assert.Single(demo, j => j.GetProperty("status").GetString() == "Planned");
    }
}
