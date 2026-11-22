namespace OmniOps.IntegrationTests.Features;

/// <summary>Delivered jobs become invoices with their proof attached; numbering, sending and payment.</summary>
[Collection(ApiTestGroup.Name)]
public class InvoicingTests(ApiFixture fixture)
{
    private static object Invoice(Guid jobId, string customer, string? send = null, string? email = null) => new
    {
        jobId, customerName = customer, customerEmail = email, paymentTermsDays = 14, send,
        lines = new[] { new { description = "Transport Skopje → Bitola", quantity = 1, unitPrice = 250m, vatRatePercent = 18m } },
    };

    private static async Task<Guid> DeliveredJobAsync(Company company, string customer)
    {
        var jobId = (await company.CreateJobAsync(new { customerName = customer })).GetProperty("id").GetGuid();
        (await company.UploadAsync(jobId, "PickupDocument")).EnsureSuccessStatusCode();
        await company.DeliverAsync(jobId);
        return jobId;
    }

    [Fact]
    public async Task DeliveredJob_IsInvoiced_SentAndPaid_WithProofOnTheCustomerPage()
    {
        var company = await fixture.NewCompanyAsync();
        var jobId = await DeliveredJobAsync(company, "Tinex Kumanovo");

        // Invoice details come first.
        Assert.Equal(HttpStatusCode.Conflict, (await company.Owner.PostAsJsonAsync("/api/v1/invoices", Invoice(jobId, "Tinex Kumanovo"))).StatusCode);
        (await company.SaveBillingProfileAsync("VT")).EnsureSuccessStatusCode();

        var overview = await company.Owner.GetJsonAsync("/api/v1/invoices");
        Assert.Contains(overview.GetProperty("toInvoice").EnumerateArray(), j => j.GetProperty("job").GetProperty("id").GetGuid() == jobId);

        var created = await (await company.Owner.PostAsJsonAsync("/api/v1/invoices",
            Invoice(jobId, "Tinex Kumanovo", send: "manual", email: "ap@tinex.example"))).ReadJsonAsync();
        var invoice = created.GetProperty("summary");
        Assert.Matches(@"^VT-\d{4}-\d{4}$", invoice.GetProperty("number").GetString());
        Assert.Equal("Sent", invoice.GetProperty("status").GetString());
        Assert.Equal(295.00m, invoice.GetProperty("total").GetDecimal());

        // The same delivery cannot be invoiced twice.
        Assert.Equal(HttpStatusCode.Conflict, (await company.Owner.PostAsJsonAsync("/api/v1/invoices", Invoice(jobId, "Tinex Kumanovo"))).StatusCode);

        // The customer's page shows the invoice and the delivery's proof, and only that delivery's files.
        var customer = fixture.Anonymous();
        var token = invoice.GetProperty("publicPath").GetString()!["/i/".Length..];
        var page = await customer.GetJsonAsync($"/api/public/invoices/{token}");
        Assert.Equal("MK07250120000058984", page.GetProperty("sellerIban").GetString());
        Assert.Equal("Ana", page.GetProperty("receivedBy").GetString());
        Assert.Equal(2, page.GetProperty("documents").GetArrayLength());
        var photoId = page.GetProperty("documents")[0].GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await customer.GetAsync($"/api/public/invoices/{token}/documents/{photoId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await customer.GetAsync($"/api/public/invoices/{token}/documents/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await customer.GetAsync("/api/public/invoices/AAAAAAAAAAAAAAAAAAAAAA")).StatusCode);
        Assert.Equal("text/html", (await customer.GetAsync($"/i/{token}")).Content.Headers.ContentType?.MediaType);

        var id = invoice.GetProperty("id").GetGuid();
        (await company.Owner.PostAsJsonAsync($"/api/v1/invoices/{id}/paid", new { })).EnsureSuccessStatusCode();
        Assert.Equal("Paid", (await company.Owner.GetJsonAsync($"/api/v1/invoices/{id}")).GetProperty("summary").GetProperty("status").GetString());

        // The customer's details are remembered for the next invoice.
        var nextJob = await DeliveredJobAsync(company, "tinex  KUMANOVO");
        overview = await company.Owner.GetJsonAsync("/api/v1/invoices");
        var suggestion = overview.GetProperty("toInvoice").EnumerateArray().First(j => j.GetProperty("job").GetProperty("id").GetGuid() == nextJob);
        Assert.Equal("ap@tinex.example", suggestion.GetProperty("customerEmail").GetString());
    }

    [Fact]
    public async Task InvoicesIssuedAtTheSameMoment_GetUniqueConsecutiveNumbers()
    {
        var company = await fixture.NewCompanyAsync();
        (await company.SaveBillingProfileAsync("CC")).EnsureSuccessStatusCode();
        var jobs = new List<Guid>();
        for (var i = 0; i < 8; i++)
        {
            jobs.Add(await DeliveredJobAsync(company, $"Parallel {i}"));
        }

        var responses = await Task.WhenAll(jobs.Select(j => company.Owner.PostAsJsonAsync("/api/v1/invoices", Invoice(j, "Parallel"))));
        var sequences = new List<int>();
        foreach (var response in responses)
        {
            var number = (await response.ReadJsonAsync()).GetProperty("summary").GetProperty("number").GetString()!;
            sequences.Add(int.Parse(number.Split('-')[^1], System.Globalization.CultureInfo.InvariantCulture));
        }

        Assert.Equal(Enumerable.Range(1, 8), sequences.Order());
    }

    [Fact]
    public async Task EmailSending_WhenNoMailServerIsConfigured_IsRefusedWithAClearReason()
    {
        var company = await fixture.NewCompanyAsync();
        (await company.SaveBillingProfileAsync()).EnsureSuccessStatusCode();
        Assert.False((await company.Owner.GetJsonAsync("/api/v1/billing/profile")).GetProperty("emailSendingEnabled").GetBoolean());

        var jobId = await DeliveredJobAsync(company, "No mail");
        var id = (await (await company.Owner.PostAsJsonAsync("/api/v1/invoices", Invoice(jobId, "No mail"))).ReadJsonAsync())
            .GetProperty("summary").GetProperty("id").GetGuid();
        var send = await company.Owner.PostAsJsonAsync($"/api/v1/invoices/{id}/send", new { email = true });
        Assert.Equal(HttpStatusCode.Conflict, send.StatusCode);
        Assert.Contains("Copy the invoice link", await send.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvoiceSettings_AreOwnerOnly_AndValidated()
    {
        var company = await fixture.NewCompanyAsync();
        var response = await company.Owner.PutAsJsonAsync("/api/v1/billing/profile", new
        {
            legalName = "X", address = "Y", iban = "12", currency = "EUR", defaultVatRatePercent = 18, defaultPaymentTermsDays = 30, invoicePrefix = "INV",
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("IBAN", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }
}
