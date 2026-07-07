using System.Net.Http.Json;
using System.Text.Json;
using OmniOps.Core.Entities;

namespace OmniOps.Api.Tests.Integration;

[Collection(OmniOpsApiCollection.Name)]
public class TenantAdminIntegrationTests(OmniOpsApiFixture fixture)
{
    [Fact]
    public async Task GetWorkspaces_ReturnsDefaultWorkspace()
    {
        var workspaces = await fixture.Client.GetFromJsonAsync<JsonElement[]>(
            $"/api/v1/tenant/organizations/{TenantSeed.DefaultOrganizationId}/workspaces");
        Assert.NotNull(workspaces);
        Assert.Contains(workspaces, w => w.GetProperty("id").GetGuid() == TenantSeed.DefaultWorkspaceId);
    }

    [Fact]
    public async Task GetTenantSettings_ReturnsDefaults()
    {
        var settings = await fixture.Client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/tenant/organizations/{TenantSeed.DefaultOrganizationId}/settings");
        Assert.Equal("UTC", settings.GetProperty("timeZone").GetString());
    }

    [Fact]
    public async Task GetAuditLogs_ReturnsList()
    {
        var logs = await fixture.Client.GetFromJsonAsync<JsonElement[]>("/api/v1/admin/audit-logs");
        Assert.NotNull(logs);
    }

    [Fact]
    public async Task GetApiKeys_ReturnsList()
    {
        var keys = await fixture.Client.GetFromJsonAsync<JsonElement[]>("/api/v1/admin/api-keys");
        Assert.NotNull(keys);
    }

    [Fact]
    public async Task GetNotifications_ReturnsList()
    {
        var notifications = await fixture.Client.GetFromJsonAsync<JsonElement[]>("/api/v1/notifications?limit=10");
        Assert.NotNull(notifications);
    }
}
