namespace OmniOps.IntegrationTests.Features;

/// <summary>Sign-up, sign-in, driver phones and sessions.</summary>
[Collection(ApiTestGroup.Name)]
public class AccountTests(ApiFixture fixture)
{
    [Fact]
    public async Task Requests_WithoutSignIn_AreRefused()
    {
        var anonymous = fixture.Anonymous();
        foreach (var url in new[] { "/api/v1/jobs", "/api/v1/invoices", "/api/v1/vehicles", "/api/v1/drivers", "/api/v1/audit" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(url)).StatusCode);
        }
    }

    [Fact]
    public async Task ATamperedToken_IsRefused()
    {
        var company = await fixture.NewCompanyAsync();
        var token = company.Session.GetProperty("accessToken").GetString()!;
        var forged = fixture.SignedIn(token[..^4] + "AAAA");
        Assert.Equal(HttpStatusCode.Unauthorized, (await forged.GetAsync("/api/v1/jobs")).StatusCode);
    }

    [Fact]
    public async Task SignUp_TwiceWithTheSameEmail_IsRefused()
    {
        var company = await fixture.NewCompanyAsync();
        var again = await fixture.Anonymous().PostAsJsonAsync("/api/v1/account/sign-up",
            new { companyName = "Again", name = "Owner", email = company.Email.ToUpperInvariant(), password = Company.Password });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task SignIn_WithAWrongPassword_IsRefused_AndLocksAfterFiveTries()
    {
        var company = await fixture.NewCompanyAsync();
        var anonymous = fixture.Anonymous();
        (await anonymous.PostAsJsonAsync("/api/v1/account/sign-in", new { email = company.Email.ToUpperInvariant(), password = Company.Password }))
            .EnsureSuccessStatusCode();

        for (var i = 0; i < 5; i++)
        {
            var wrong = await anonymous.PostAsJsonAsync("/api/v1/account/sign-in", new { email = company.Email, password = "wrong password 1" });
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        var locked = await anonymous.PostAsJsonAsync("/api/v1/account/sign-in", new { email = company.Email, password = Company.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Assert.Contains("15 minutes", await locked.Content.ReadAsStringAsync());

        var unknown = await anonymous.PostAsJsonAsync("/api/v1/account/sign-in", new { email = "nobody@example.test", password = "whatever 123" });
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
    }

    [Fact]
    public async Task Driver_PairsWithACode_AndOnlyEverSeesTheirOwnJobs()
    {
        var company = await fixture.NewCompanyAsync();
        var driverId = await company.AddDriverAsync("Paired Driver");
        var mine = (await company.CreateJobAsync(driverId: driverId)).GetProperty("id").GetGuid();
        var someoneElses = (await company.CreateJobAsync()).GetProperty("id").GetGuid();

        var code = (await (await company.Owner.PostAsJsonAsync("/api/v1/account/driver-codes", new { driverId })).ReadJsonAsync())
            .GetProperty("code").GetString()!;
        Assert.Matches("^[A-Z2-9]{4}-[A-Z2-9]{4}$", code);

        var anonymous = fixture.Anonymous();
        var session = await (await anonymous.PostAsJsonAsync("/api/v1/account/pair", new { code = code.ToLowerInvariant().Replace("-", " ", StringComparison.Ordinal) })).ReadJsonAsync();
        Assert.Equal("Driver", session.GetProperty("account").GetProperty("role").GetString());
        var phone = fixture.SignedIn(session.GetProperty("accessToken").GetString()!);

        // Single use.
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/v1/account/pair", new { code })).StatusCode);

        var jobs = await phone.GetFromJsonAsync<JsonElement[]>("/api/v1/driver/jobs");
        Assert.Equal(mine, Assert.Single(jobs!).GetProperty("id").GetGuid());
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
        var company = await fixture.NewCompanyAsync();
        var anonymous = fixture.Anonymous();
        var refreshToken = company.Session.GetProperty("refreshToken").GetString();

        var next = await (await anonymous.PostAsJsonAsync("/api/v1/account/refresh", new { refreshToken })).ReadJsonAsync();
        (await fixture.SignedIn(next.GetProperty("accessToken").GetString()!).GetAsync("/api/v1/account/me")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/v1/account/refresh", new { refreshToken })).StatusCode);

        var nextRefresh = next.GetProperty("refreshToken").GetString();
        (await anonymous.PostAsJsonAsync("/api/v1/account/sign-out", new { refreshToken = nextRefresh })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/v1/account/refresh", new { refreshToken = nextRefresh })).StatusCode);
    }

    [Fact]
    public async Task DemoCompany_IsCreatedOnce_AndOnlyItCanLoadADemoDay()
    {
        var anonymous = fixture.Anonymous();
        var first = await (await anonymous.PostAsync("/api/v1/account/demo", null)).ReadJsonAsync();
        var second = await (await anonymous.PostAsync("/api/v1/account/demo", null)).ReadJsonAsync();
        Assert.Equal(first.GetProperty("account").GetProperty("organizationId").GetGuid(), second.GetProperty("account").GetProperty("organizationId").GetGuid());

        var demo = fixture.SignedIn(second.GetProperty("accessToken").GetString()!);
        Assert.True((await demo.GetJsonAsync("/api/v1/demo/status")).GetProperty("isDemoCompany").GetBoolean());
        var day = await (await demo.PostAsync("/api/v1/demo/day", null)).ReadJsonAsync();
        Assert.True(day.GetProperty("trucksOnTheWay").GetInt32() >= 2);
        Assert.Contains((await demo.GetFromJsonAsync<JsonElement[]>("/api/v1/jobs"))!, j => j.GetProperty("reference").GetString() == "DEMO-1042");

        var company = await fixture.NewCompanyAsync();
        Assert.False((await company.Owner.GetJsonAsync("/api/v1/demo/status")).GetProperty("isDemoCompany").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await company.Owner.PostAsync("/api/v1/demo/day", null)).StatusCode);
    }
}
