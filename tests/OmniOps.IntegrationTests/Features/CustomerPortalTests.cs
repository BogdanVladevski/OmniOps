namespace OmniOps.IntegrationTests.Features;

/// <summary>Customers, their portal link, and the proof pack: each customer sees their own deliveries and nothing else.</summary>
[Collection(ApiTestGroup.Name)]
public class CustomerPortalTests(ApiFixture fixture)
{
    private static async Task<Guid> CustomerIdAsync(Company company, Guid jobId) =>
        (await company.Owner.GetJsonAsync($"/api/v1/jobs/{jobId}")).GetProperty("job").GetProperty("customerId").GetGuid();

    private static async Task<string> PortalTokenAsync(Company company, Guid customerId)
    {
        var customer = await (await company.Owner.PostAsync($"/api/v1/customers/{customerId}/portal", null)).ReadJsonAsync();
        return customer.GetProperty("portalUrl").GetString()!.Split("/c/")[1];
    }

    [Fact]
    public async Task Jobs_for_the_same_name_belong_to_one_customer()
    {
        var company = await fixture.NewCompanyAsync();
        var first = (await company.CreateJobAsync(new { customerName = "Tinex Kumanovo" })).GetProperty("id").GetGuid();
        var second = (await company.CreateJobAsync(new { customerName = "  tinex   KUMANOVO " })).GetProperty("id").GetGuid();
        await company.CreateJobAsync(new { customerName = "Vero Bitola" });

        Assert.Equal(await CustomerIdAsync(company, first), await CustomerIdAsync(company, second));
        var customers = (await company.Owner.GetJsonAsync("/api/v1/customers")).EnumerateArray().ToList();
        Assert.Equal(2, customers.Count);
        var tinex = customers.Single(c => c.GetProperty("name").GetString() == "Tinex Kumanovo");
        Assert.Equal(2, tinex.GetProperty("activeJobs").GetInt32());
        Assert.Equal(JsonValueKind.Null, tinex.GetProperty("portalUrl").ValueKind);
    }

    [Fact]
    public async Task The_portal_shows_only_that_customers_deliveries()
    {
        var company = await fixture.NewCompanyAsync();
        var mine = (await company.CreateJobAsync(new { customerName = "Tinex Kumanovo", reference = "TX-1" })).GetProperty("id").GetGuid();
        await company.CreateJobAsync(new { customerName = "Vero Bitola", reference = "VR-1" });
        var other = await fixture.NewCompanyAsync("Other Haulage");
        await other.CreateJobAsync(new { customerName = "Tinex Kumanovo", reference = "OTHER-1" });

        var token = await PortalTokenAsync(company, await CustomerIdAsync(company, mine));
        var portal = await fixture.Anonymous().GetJsonAsync($"/api/public/portal/{token}");

        Assert.Equal("Test Haulage", portal.GetProperty("companyName").GetString());
        Assert.Equal("Tinex Kumanovo", portal.GetProperty("customerName").GetString());
        var planned = portal.GetProperty("planned").EnumerateArray().ToList();
        Assert.Equal(["TX-1"], planned.Select(j => j.GetProperty("reference").GetString()));
        Assert.Empty(portal.GetProperty("delivered").EnumerateArray());
    }

    [Fact]
    public async Task Replacing_or_turning_off_the_link_stops_the_old_one()
    {
        var company = await fixture.NewCompanyAsync();
        var customerId = await CustomerIdAsync(company, (await company.CreateJobAsync()).GetProperty("id").GetGuid());
        var anonymous = fixture.Anonymous();

        var first = await PortalTokenAsync(company, customerId);
        var second = await PortalTokenAsync(company, customerId);

        Assert.NotEqual(first, second);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/public/portal/{first}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/api/public/portal/{second}")).StatusCode);

        (await company.Owner.DeleteAsync($"/api/v1/customers/{customerId}/portal")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/public/portal/{second}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/api/public/portal/not-a-token")).StatusCode);
    }

    [Fact]
    public async Task A_delivered_job_has_a_proof_pdf_for_its_customer_only()
    {
        var company = await fixture.NewCompanyAsync();
        var delivered = (await company.CreateJobAsync(new { customerName = "Tinex Kumanovo", minTempCelsius = 2.0, maxTempCelsius = 8.0 })).GetProperty("id").GetGuid();
        (await company.Owner.PostAsync($"/api/v1/driver/jobs/{delivered}/start", null)).EnsureSuccessStatusCode();
        (await company.UploadAsync(delivered, "TemperaturePhoto", new Dictionary<string, string> { ["temperatureCelsius"] = "4.5" })).EnsureSuccessStatusCode();
        await company.DeliverAsync(delivered);
        var open = (await company.CreateJobAsync(new { customerName = "Tinex Kumanovo" })).GetProperty("id").GetGuid();
        var someoneElses = (await company.CreateJobAsync(new { customerName = "Vero Bitola" })).GetProperty("id").GetGuid();
        await company.DeliverAsync(someoneElses);

        var token = await PortalTokenAsync(company, await CustomerIdAsync(company, delivered));
        var anonymous = fixture.Anonymous();
        var portal = await anonymous.GetJsonAsync($"/api/public/portal/{token}");
        var shown = portal.GetProperty("delivered").EnumerateArray().Single();
        Assert.Equal("Ana", shown.GetProperty("receivedBy").GetString());
        Assert.Equal(1, shown.GetProperty("temperatureReadings").GetInt32());

        var pdf = await anonymous.GetAsync(shown.GetProperty("proofPath").GetString());
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.Equal("inline", pdf.Content.Headers.ContentDisposition?.DispositionType);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(await pdf.Content.ReadAsByteArrayAsync())[..4]);

        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/public/portal/{token}/jobs/{open}/proof.pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/public/portal/{token}/jobs/{someoneElses}/proof.pdf")).StatusCode);

        // The office gets the pack for any of its jobs; another company gets nothing.
        Assert.Equal(HttpStatusCode.OK, (await company.Owner.GetAsync($"/api/v1/jobs/{someoneElses}/proof.pdf")).StatusCode);
        var other = await fixture.NewCompanyAsync("Other Haulage");
        Assert.Equal(HttpStatusCode.NotFound, (await other.Owner.GetAsync($"/api/v1/jobs/{delivered}/proof.pdf")).StatusCode);
    }

    [Fact]
    public async Task Invoices_for_the_customers_deliveries_appear_on_the_portal()
    {
        var company = await fixture.NewCompanyAsync();
        (await company.SaveBillingProfileAsync()).EnsureSuccessStatusCode();
        var jobId = (await company.CreateJobAsync(new { customerName = "Tinex Kumanovo" })).GetProperty("id").GetGuid();
        await company.DeliverAsync(jobId);
        (await company.Owner.PostAsJsonAsync("/api/v1/invoices", new
        {
            jobId, customerName = "Tinex DOOEL Kumanovo", customerEmail = (string?)null, customerAddress = (string?)null, customerTaxId = (string?)null,
            paymentTermsDays = 15, notes = (string?)null, send = "manual",
            lines = new[] { new { description = "Delivery", quantity = 1, unitPrice = 250m, vatRatePercent = 18m } },
        })).EnsureSuccessStatusCode();

        var token = await PortalTokenAsync(company, await CustomerIdAsync(company, jobId));
        var invoice = (await fixture.Anonymous().GetJsonAsync($"/api/public/portal/{token}")).GetProperty("invoices").EnumerateArray().Single();

        Assert.Equal(295.00m, invoice.GetProperty("total").GetDecimal());
        Assert.Equal("Unpaid", invoice.GetProperty("status").GetString());
        var customers = await company.Owner.GetJsonAsync("/api/v1/customers");
        Assert.Equal(295.00m, customers.EnumerateArray().Single().GetProperty("outstanding").GetDecimal());
    }

    [Fact]
    public async Task Customer_details_are_checked_and_kept()
    {
        var company = await fixture.NewCompanyAsync();
        var customerId = await CustomerIdAsync(company, (await company.CreateJobAsync()).GetProperty("id").GetGuid());
        object Body(string? email, bool updates, string phone = "+389 70 111 222") =>
            new { name = "Bitola Market", email, phone, address = "Bitola", taxId = (string?)null, paymentTermsDays = 30, emailUpdates = updates };

        Assert.Equal(HttpStatusCode.BadRequest, (await company.Owner.PutAsJsonAsync($"/api/v1/customers/{customerId}", Body(null, updates: true))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await company.Owner.PutAsJsonAsync($"/api/v1/customers/{customerId}", Body("x@y.mk", false, phone: "call me"))).StatusCode);
        var saved = await (await company.Owner.PutAsJsonAsync($"/api/v1/customers/{customerId}", Body("orders@bitola.example", updates: true))).ReadJsonAsync();
        Assert.True(saved.GetProperty("emailUpdates").GetBoolean());
        Assert.Equal("+389 70 111 222", saved.GetProperty("phone").GetString());

        var other = await fixture.NewCompanyAsync("Other Haulage");
        Assert.Equal(HttpStatusCode.NotFound, (await other.Owner.GetAsync($"/api/v1/customers/{customerId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Owner.PostAsync($"/api/v1/customers/{customerId}/portal", null)).StatusCode);
    }

    [Fact]
    public async Task Drivers_cannot_reach_customers_or_proof_packs()
    {
        var company = await fixture.NewCompanyAsync();
        var driverId = await company.AddDriverAsync();
        var jobId = (await company.CreateJobAsync(driverId: driverId)).GetProperty("id").GetGuid();
        var (phone, _) = await company.PairDriverAsync(driverId);

        Assert.Equal(HttpStatusCode.Forbidden, (await phone.GetAsync("/api/v1/customers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await phone.GetAsync($"/api/v1/jobs/{jobId}/proof.pdf")).StatusCode);
    }
}
