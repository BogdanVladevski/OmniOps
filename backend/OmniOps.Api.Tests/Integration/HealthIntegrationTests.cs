namespace OmniOps.Api.Tests.Integration;

[Collection(OmniOpsApiCollection.Name)]
public class HealthIntegrationTests(OmniOpsApiFixture fixture)
{
    [Fact]
    public async Task Live_ReturnsHealthy()
    {
        var response = await fixture.Client.GetAsync("/health/live");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ready_ReturnsHealthy_WhenDependenciesAreUp()
    {
        var response = await fixture.Client.GetAsync("/health/ready");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", body, StringComparison.OrdinalIgnoreCase);
    }
}
