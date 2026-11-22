using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OmniOps.Api.Tests.Integration;

/// <summary>Sign-up, sign-in and — most importantly — that one company can never reach another's data.</summary>
[Collection(OmniOpsApiCollection.Name)]
public class AccountIntegrationTests(OmniOpsApiFixture fixture)
{
    private static readonly Guid DemoFleetId = Guid.Parse("f1000000-0000-0000-0000-000000000001");
    private HttpClient Demo => fixture.Client;

    private async Task<(HttpClient Client, JsonElement Session)> SignUpAsync(string company)
    {
        var email = $"{Guid.NewGuid():N}@example.test";
        var response = await fixture.AnonymousClient().PostAsJsonAsync("/api/v1/account/sign-up",
            new { companyName = company, name = "Owner", email, password = "correct horse battery" });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var session = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (fixture.ClientFor(session.GetProperty("accessToken").GetString()!), session);
    }

    private async Task<Guid> DemoJobAsync(Guid? driverId = null)
    {
        var assignable = await Demo.GetFromJsonAsync<JsonElement>("/api/v1/jobs/assignable");
        var vehicleId = assignable.GetProperty("vehicles")[0].GetProperty("id").GetGuid();
        var created = await Demo.PostAsJsonAsync("/api/v1/jobs", new { vehicleId, driverId, customerName = "Secret customer", destination = "Bitola" });
        created.EnsureSuccessStatusCode();
        return (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task NewCompany_StartsEmpty_AndCannotReachAnotherCompanysData()
    {
        var demoJob = await DemoJobAsync();
        var (other, session) = await SignUpAsync("Other Haulage");
        var ownFleet = session.GetProperty("account").GetProperty("defaultFleetId").GetGuid();

        // Its own, empty world.
        var fleets = await other.GetFromJsonAsync<JsonElement[]>("/api/fleets");
        Assert.Equal(ownFleet, Assert.Single(fleets!).GetProperty("id").GetGuid());
        Assert.Empty((await other.GetFromJsonAsync<JsonElement[]>("/api/v1/jobs?scope=all"))!);
        Assert.Empty((await other.GetFromJsonAsync<JsonElement>("/api/v1/jobs/assignable")).GetProperty("vehicles").EnumerateArray());
        Assert.Empty((await other.GetFromJsonAsync<JsonElement>("/api/telemetry/fleet")).GetProperty("vehicles").EnumerateArray());
        Assert.DoesNotContain("Secret customer", await other.GetStringAsync("/api/v1/admin/audit-logs"));
        var status = await other.GetFromJsonAsync<JsonElement>("/api/v1/demo/status");
        Assert.Equal("Other Haulage", status.GetProperty("organizationName").GetString());
        Assert.False(status.GetProperty("isDemoOrganization").GetBoolean());

        // The demo company's things do not exist for it — reading or writing.
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/jobs/{demoJob}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/v1/driver/jobs/{demoJob}/start", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/fleets/{DemoFleetId}/statistics")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/fleets/{DemoFleetId}/vehicles")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/incidents?fleetId={DemoFleetId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync("/api/predictions/vehicles/Truck-001/health")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync("/api/vehicles",
            new { fleetId = DemoFleetId, externalId = "Planted-1" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync("/api/drivers",
            new { fleetId = DemoFleetId, fullName = "Planted driver" })).StatusCode);

        var demoOverview = await Demo.GetFromJsonAsync<JsonElement>("/api/v1/invoices");
        Assert.Empty((await other.GetFromJsonAsync<JsonElement>("/api/v1/invoices")).GetProperty("invoices").EnumerateArray());
        foreach (var invoice in demoOverview.GetProperty("invoices").EnumerateArray())
        {
            Assert.Equal(HttpStatusCode.NotFound,
                (await other.GetAsync($"/api/v1/invoices/{invoice.GetProperty("id").GetGuid()}")).StatusCode);
        }
    }

    [Fact]
    public async Task NewCompany_CanRunItsOwnFleet()
    {
        var (other, session) = await SignUpAsync("Own Fleet DOO");
        var fleetId = session.GetProperty("account").GetProperty("defaultFleetId").GetGuid();

        // A truck with the same name as the demo company's must not mix with it.
        (await other.PostAsJsonAsync("/api/vehicles", new { fleetId, externalId = "Truck-001", registration = "SK-1234-AB" })).EnsureSuccessStatusCode();
        (await other.PostAsJsonAsync("/api/drivers", new { fleetId, fullName = "Marko" })).EnsureSuccessStatusCode();

        var assignable = await other.GetFromJsonAsync<JsonElement>("/api/v1/jobs/assignable");
        Assert.Equal("SK-1234-AB", Assert.Single(assignable.GetProperty("vehicles").EnumerateArray()).GetProperty("detail").GetString());
        Assert.Single(assignable.GetProperty("drivers").EnumerateArray());
    }

    [Fact]
    public async Task Requests_WithoutSignIn_AreRefused()
    {
        var anonymous = fixture.AnonymousClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/jobs")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/invoices")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/fleets")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync("/api/v1/demo/bootstrap", null)).StatusCode);
    }

    [Fact]
    public async Task SignIn_WithAWrongPassword_IsRefused_AndLocksAfterFiveTries()
    {
        var email = $"{Guid.NewGuid():N}@example.test";
        var anonymous = fixture.AnonymousClient();
        (await anonymous.PostAsJsonAsync("/api/v1/account/sign-up",
            new { companyName = "Lock Test", name = "Owner", email, password = "correct horse battery" })).EnsureSuccessStatusCode();

        var duplicate = await anonymous.PostAsJsonAsync("/api/v1/account/sign-up",
            new { companyName = "Again", name = "Owner", email, password = "correct horse battery" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        (await anonymous.PostAsJsonAsync("/api/v1/account/sign-in", new { email = email.ToUpperInvariant(), password = "correct horse battery" }))
            .EnsureSuccessStatusCode();

        for (var i = 0; i < 5; i++)
        {
            var wrong = await anonymous.PostAsJsonAsync("/api/v1/account/sign-in", new { email, password = "wrong password 1" });
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        var locked = await anonymous.PostAsJsonAsync("/api/v1/account/sign-in", new { email, password = "correct horse battery" });
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Assert.Contains("15 minutes", await locked.Content.ReadAsStringAsync());

        var unknown = await anonymous.PostAsJsonAsync("/api/v1/account/sign-in", new { email = "nobody@example.test", password = "whatever 123" });
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
    }

    [Fact]
    public async Task Driver_PairsWithACode_AndOnlyEverSeesTheirOwnJobs()
    {
        var driver = await Demo.PostAsJsonAsync("/api/drivers", new { fleetId = DemoFleetId, fullName = "Paired Driver" });
        driver.EnsureSuccessStatusCode();
        var driverId = (await driver.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var mine = await DemoJobAsync(driverId);
        var someoneElses = await DemoJobAsync();

        var code = (await (await Demo.PostAsJsonAsync("/api/v1/account/driver-codes", new { driverId }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;
        Assert.Matches("^[A-Z2-9]{4}-[A-Z2-9]{4}$", code);

        var anonymous = fixture.AnonymousClient();
        var paired = await anonymous.PostAsJsonAsync("/api/v1/account/pair", new { code = code.ToLowerInvariant().Replace("-", " ") });
        paired.EnsureSuccessStatusCode();
        var session = await paired.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Driver", session.GetProperty("account").GetProperty("role").GetString());
        var phone = fixture.ClientFor(session.GetProperty("accessToken").GetString()!);

        // Single use.
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/v1/account/pair", new { code })).StatusCode);

        var jobs = await phone.GetFromJsonAsync<JsonElement[]>($"/api/v1/driver/jobs?driverId={Guid.NewGuid()}");
        Assert.Contains(jobs!, j => j.GetProperty("id").GetGuid() == mine);
        Assert.DoesNotContain(jobs!, j => j.GetProperty("id").GetGuid() == someoneElses);
        Assert.All(jobs!, j => Assert.Equal(driverId, j.GetProperty("driverId").GetGuid()));

        (await phone.PostAsync($"/api/v1/driver/jobs/{mine}/start", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await phone.PostAsync($"/api/v1/driver/jobs/{someoneElses}/start", null)).StatusCode);

        // A driver's phone has no office access.
        Assert.Equal(HttpStatusCode.Forbidden, (await phone.GetAsync("/api/v1/jobs")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await phone.GetAsync("/api/v1/invoices")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await phone.PostAsJsonAsync("/api/v1/account/driver-codes", new { driverId })).StatusCode);
    }

    [Fact]
    public async Task RefreshTokens_AreSingleUse()
    {
        var (_, session) = await SignUpAsync("Refresh Test");
        var anonymous = fixture.AnonymousClient();
        var refreshToken = session.GetProperty("refreshToken").GetString();

        var renewed = await anonymous.PostAsJsonAsync("/api/v1/account/refresh", new { refreshToken });
        renewed.EnsureSuccessStatusCode();
        var next = await renewed.Content.ReadFromJsonAsync<JsonElement>();
        (await fixture.ClientFor(next.GetProperty("accessToken").GetString()!).GetAsync("/api/v1/account/me")).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/v1/account/refresh", new { refreshToken })).StatusCode);

        (await anonymous.PostAsJsonAsync("/api/v1/account/sign-out", new { refreshToken = next.GetProperty("refreshToken").GetString() })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsJsonAsync("/api/v1/account/refresh", new { refreshToken = next.GetProperty("refreshToken").GetString() })).StatusCode);
    }

    [Fact]
    public async Task DriverPhone_IsKept_ShownOnJobs_AndOnlyEditableByTheirCompany()
    {
        var (company, session) = await SignUpAsync("Phone Co");
        var fleetId = session.GetProperty("account").GetProperty("defaultFleetId").GetGuid();
        var bad = await company.PostAsJsonAsync("/api/drivers", new { fleetId, fullName = "Ana", phone = "call me" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        var driver = await (await company.PostAsJsonAsync("/api/drivers", new { fleetId, fullName = "Ana", phone = "+389 70 123 456" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var driverId = driver.GetProperty("id").GetGuid();
        Assert.Equal("+389 70 123 456", driver.GetProperty("phone").GetString());

        var updated = await (await company.PutAsJsonAsync($"/api/drivers/{driverId}", new { fullName = "Ana Ristova", phone = "+389 71 000 111" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("+389 71 000 111", updated.GetProperty("phone").GetString());

        var (other, _) = await SignUpAsync("Other Phone Co");
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/drivers/{driverId}", new { fullName = "X", phone = "+1 555 0000" })).StatusCode);

        (await company.PostAsJsonAsync("/api/vehicles", new { fleetId, externalId = "PH-1" })).EnsureSuccessStatusCode();
        var assignable = await company.GetFromJsonAsync<JsonElement>("/api/v1/jobs/assignable");
        Assert.Equal("+389 71 000 111", assignable.GetProperty("drivers")[0].GetProperty("detail").GetString());
        var job = await (await company.PostAsJsonAsync("/api/v1/jobs", new
        {
            vehicleId = assignable.GetProperty("vehicles")[0].GetProperty("id").GetGuid(), driverId, customerName = "C", destination = "D",
        })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("+389 71 000 111", job.GetProperty("driverPhone").GetString());
    }
}
