using Microsoft.EntityFrameworkCore;
using OmniOps.Application.Features.Jobs;

namespace OmniOps.IntegrationTests.Features;

/// <summary>
/// One company can never see or touch another's data. Every company-owned table is filtered by the signed-in company
/// in the database layer; these tests attack it through the API the way a curious customer would.
/// </summary>
[Collection(ApiTestGroup.Name)]
public class CompanyIsolationTests(ApiFixture fixture)
{
    [Fact]
    public async Task AnotherCompany_SeesNothing_AndEveryIdIsNotFound()
    {
        var victim = await fixture.NewCompanyAsync("Victim Transport");
        var driverId = await victim.AddDriverAsync("Secret Driver", "+389 70 000 001");
        var job = await victim.CreateJobAsync(new { customerName = "Secret customer" }, driverId: driverId);
        var jobId = job.GetProperty("id").GetGuid();
        var vehicleId = job.GetProperty("vehicleId").GetGuid();
        await victim.DeliverAsync(jobId);
        (await victim.SaveBillingProfileAsync()).EnsureSuccessStatusCode();
        var invoice = await (await victim.Owner.PostAsJsonAsync("/api/v1/invoices", new
        {
            jobId, customerName = "Secret customer", lines = new[] { new { description = "Transport", quantity = 1, unitPrice = 100, vatRatePercent = 18 } },
        })).ReadJsonAsync();
        var invoiceId = invoice.GetProperty("summary").GetProperty("id").GetGuid();
        var documentId = (await victim.Owner.GetJsonAsync($"/api/v1/jobs/{jobId}")).GetProperty("documents")[0].GetProperty("id").GetGuid();

        var attacker = await fixture.NewCompanyAsync("Curious Transport");
        var a = attacker.Owner;

        // Lists show nothing of theirs.
        Assert.Empty((await a.GetFromJsonAsync<JsonElement[]>("/api/v1/jobs?scope=All"))!);
        Assert.Empty((await a.GetFromJsonAsync<JsonElement[]>("/api/v1/vehicles"))!);
        Assert.Empty((await a.GetFromJsonAsync<JsonElement[]>("/api/v1/drivers"))!);
        var overview = await a.GetJsonAsync("/api/v1/invoices");
        Assert.Empty(overview.GetProperty("invoices").EnumerateArray());
        Assert.Empty(overview.GetProperty("toInvoice").EnumerateArray());
        Assert.DoesNotContain("Secret", await a.GetStringAsync("/api/v1/audit"), StringComparison.Ordinal);

        // Their ids are "not found", for reading and for writing.
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync($"/api/v1/jobs/{jobId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync($"/api/v1/jobs/{jobId}/documents/{documentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync($"/api/v1/invoices/{invoiceId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.PostAsJsonAsync($"/api/v1/invoices/{invoiceId}/paid", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.PostAsync($"/api/v1/driver/jobs/{jobId}/start", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.PutAsJsonAsync($"/api/v1/drivers/{driverId}", new { fullName = "Hijacked" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.PostAsJsonAsync("/api/v1/account/driver-codes", new { driverId })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.PostAsJsonAsync("/api/v1/jobs", new
        {
            vehicleId, driverId, customerName = "Planted", destination = "Anywhere",
        })).StatusCode);
    }

    [Fact]
    public async Task TheDatabaseFilter_ShowsAnonymousQueriesNothing()
    {
        var company = await fixture.NewCompanyAsync();
        await company.CreateJobAsync();

        // Outside any sign-in (as a background job or public page would be), filtered sets are empty unless the
        // code explicitly looks across companies.
        await fixture.WithDatabaseAsync(async db =>
        {
            Assert.Equal(0, await db.Jobs.CountAsync());
            Assert.True(await db.Jobs.IgnoreQueryFilters().AnyAsync(j => j.OrganizationId == company.OrganizationId));
        });
    }

    [Fact]
    public async Task SameTruckNames_InTwoCompanies_NeverMix()
    {
        var first = await fixture.NewCompanyAsync();
        var second = await fixture.NewCompanyAsync();
        var a = (await first.CreateJobAsync(vehicleId: await first.AddVehicleAsync("Truck-1"))).GetProperty("id").GetGuid();
        await second.AddVehicleAsync("Truck-1");

        (await first.Owner.PostAsync($"/api/v1/driver/jobs/{a}/start", null)).EnsureSuccessStatusCode();
        (await first.Owner.PostAsJsonAsync($"/api/v1/driver/jobs/{a}/locations", new
        {
            points = new[] { new LocationPoint(Guid.NewGuid(), 41.5, 21.5, 50, null, 10, DateTimeOffset.UtcNow) },
        })).EnsureSuccessStatusCode();

        Assert.Empty((await second.Owner.GetFromJsonAsync<JsonElement[]>("/api/v1/jobs"))!);
        Assert.Equal(41.5, (await first.Owner.GetJsonAsync($"/api/v1/jobs/{a}")).GetProperty("job").GetProperty("lastLatitude").GetDouble());
    }
}
