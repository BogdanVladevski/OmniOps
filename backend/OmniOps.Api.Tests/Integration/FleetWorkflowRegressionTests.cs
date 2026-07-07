using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

namespace OmniOps.Api.Tests.Integration;

/// <summary>
/// End-to-end regression covering the primary operator workflow without background workers.
/// </summary>
[Collection(OmniOpsApiCollection.Name)]
public class FleetWorkflowRegressionTests(OmniOpsApiFixture fixture)
{
    private static readonly Guid DefaultFleetId = Guid.Parse("f1000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task FullOperatorWorkflow_Succeeds()
    {
        var health = await fixture.Client.GetAsync("/health/ready");
        health.EnsureSuccessStatusCode();

        var fleets = await fixture.Client.GetFromJsonAsync<JsonElement[]>("/api/fleets");
        Assert.NotEmpty(fleets!);

        var vehicles = await fixture.Client.GetFromJsonAsync<JsonElement[]>(
            $"/api/fleets/{DefaultFleetId}/vehicles");
        Assert.NotEmpty(vehicles!);

        var simulate = await fixture.Client.PostAsync(
            "/api/test/simulate/Truck-001?packets=2",
            content: null);
        simulate.EnsureSuccessStatusCode();

        var incidents = await fixture.Client.GetFromJsonAsync<JsonElement[]>(
            $"/api/incidents?fleetId={DefaultFleetId}");
        Assert.NotNull(incidents);

        var to = DateTime.UtcNow;
        var from = to.AddHours(-1);
        var analytics = await fixture.Client.GetAsync(
            $"/api/analytics/fleet/{DefaultFleetId}?fromUtc={from:O}&toUtc={to:O}");
        analytics.EnsureSuccessStatusCode();

        var copilot = await fixture.Client.PostAsJsonAsync("/api/copilot/ask", new
        {
            question = "Summarize fleet risk",
            fleetId = DefaultFleetId,
        });
        copilot.EnsureSuccessStatusCode();

        var sync = await fixture.Client.GetAsync("/api/v1/mobile/sync");
        sync.EnsureSuccessStatusCode();
    }
}

[Collection(OmniOpsApiCollection.Name)]
public class ApiLoadTests(OmniOpsApiFixture fixture)
{
    [Fact]
    public async Task HealthEndpoint_HandlesConcurrentLoad()
    {
        const int concurrency = 40;
        var sw = Stopwatch.StartNew();
        var tasks = Enumerable.Range(0, concurrency)
            .Select(_ => fixture.Client.GetAsync("/health/live"))
            .ToArray();
        var responses = await Task.WhenAll(tasks);
        sw.Stop();

        Assert.All(responses, r => r.EnsureSuccessStatusCode());
        Assert.True(sw.ElapsedMilliseconds < 10_000, $"Load test exceeded 10s: {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task FleetList_MedianLatencyUnderOneSecond()
    {
        var samples = new List<long>(20);
        for (var i = 0; i < 20; i++)
        {
            var sw = Stopwatch.StartNew();
            var response = await fixture.Client.GetAsync("/api/fleets");
            sw.Stop();
            response.EnsureSuccessStatusCode();
            samples.Add(sw.ElapsedMilliseconds);
        }

        samples.Sort();
        var median = samples[samples.Count / 2];
        Assert.True(median < 1000, $"Median latency {median}ms exceeds 1s budget");
    }
}

[Collection(OmniOpsApiCollection.Name)]
public class AuthIntegrationTests(OmniOpsApiFixture fixture)
{
    [Fact]
    public async Task DevTokenEndpoint_IssuesJwtWithScopes()
    {
        var response = await fixture.Client.PostAsJsonAsync("/api/auth/token", new
        {
            subject = "integration-user",
            scopes = new[] { "vehicle:read", "platform:admin" },
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("accessToken").GetString()));
    }
}
