using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace OmniOps.Api.Tests.Integration;

[Collection(OmniOpsApiCollection.Name)]
public class InvoicingIntegrationTests(OmniOpsApiFixture fixture)
{
    private HttpClient Client => fixture.Client;

    private Task SaveProfileAsync(string prefix) => Client.PutAsJsonAsync("/api/v1/billing/profile", new
    {
        legalName = "Vardar Transport DOOEL",
        address = "Partizanska 1, 1000 Skopje",
        taxId = "MK4030000000000",
        iban = "MK07 2501 2000 0058 984",
        bankName = "Halkbank",
        currency = "EUR",
        defaultVatRatePercent = 18,
        defaultPaymentTermsDays = 30,
        invoicePrefix = prefix,
    }).ContinueWith(t => t.Result.EnsureSuccessStatusCode());

    /// <summary>A delivered job with a loading note and a delivery photo.</summary>
    private async Task<(Guid JobId, Guid PhotoId)> DeliveredJobAsync(string customer)
    {
        var assignable = await Client.GetFromJsonAsync<JsonElement>("/api/v1/jobs/assignable");
        var vehicleId = assignable.GetProperty("vehicles")[1].GetProperty("id").GetGuid();
        var created = await Client.PostAsJsonAsync("/api/v1/jobs", new { vehicleId, customerName = customer, destination = "Bitola", reference = "PO-9" });
        var jobId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await Upload(jobId, "PickupDocument");
        (await Client.PostAsync($"/api/v1/driver/jobs/{jobId}/start", null)).EnsureSuccessStatusCode();
        var photo = await Upload(jobId, "DeliveryPhoto");
        (await Client.PostAsJsonAsync($"/api/v1/driver/jobs/{jobId}/deliver", new { recipientName = "Ana" })).EnsureSuccessStatusCode();
        return (jobId, (await photo.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
    }

    private async Task<HttpResponseMessage> Upload(Guid jobId, string kind)
    {
        using var form = new MultipartFormDataContent();
        var image = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0, 7]);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(image, "file", "p.jpg");
        var response = await Client.PostAsync($"/api/v1/driver/jobs/{jobId}/documents?kind={kind}", form);
        response.EnsureSuccessStatusCode();
        return response;
    }

    private Task<HttpResponseMessage> InvoiceAsync(Guid jobId, string? send = null) => Client.PostAsJsonAsync("/api/v1/invoices", new
    {
        jobId,
        customerName = "Tinex Kumanovo",
        customerEmail = "ap@tinex.example",
        lines = new[] { new { description = "Transport Skopje → Bitola", quantity = 1, unitPrice = 250.00, vatRatePercent = 18 } },
        send,
    });

    [Fact]
    public async Task DeliveredJob_IsInvoiced_SentAndPaid_WithProofOnTheCustomerPage()
    {
        await SaveProfileAsync("VT");
        var (jobId, photoId) = await DeliveredJobAsync("Tinex Kumanovo");

        var overview = await Client.GetFromJsonAsync<JsonElement>("/api/v1/invoices");
        Assert.Contains(overview.GetProperty("toInvoice").EnumerateArray(), j => j.GetProperty("job").GetProperty("id").GetGuid() == jobId);

        var response = await InvoiceAsync(jobId, send: "manual");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var invoice = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("summary");
        Assert.Matches(@"^VT-\d{4}-\d{4}$", invoice.GetProperty("number").GetString());
        Assert.Equal("Sent", invoice.GetProperty("status").GetString());
        Assert.Equal(295.00m, invoice.GetProperty("total").GetDecimal());

        // The same delivery cannot be invoiced twice.
        Assert.Equal(HttpStatusCode.Conflict, (await InvoiceAsync(jobId)).StatusCode);

        // The customer's page shows the invoice and the delivery's proof — and only that delivery's files.
        var token = invoice.GetProperty("publicPath").GetString()!["/i/".Length..];
        var page = await Client.GetFromJsonAsync<JsonElement>($"/api/public/invoices/{token}");
        Assert.Equal("MK07250120000058984", page.GetProperty("sellerIban").GetString());
        Assert.Equal("Ana", page.GetProperty("receivedBy").GetString());
        Assert.Equal(2, page.GetProperty("documents").GetArrayLength());
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync($"/api/public/invoices/{token}/documents/{photoId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/public/invoices/{token}/documents/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync("/api/public/invoices/AAAAAAAAAAAAAAAAAAAAAA")).StatusCode);
        Assert.Equal("text/html", (await Client.GetAsync($"/i/{token}")).Content.Headers.ContentType?.MediaType);

        var id = invoice.GetProperty("id").GetGuid();
        (await Client.PostAsJsonAsync($"/api/v1/invoices/{id}/paid", new { })).EnsureSuccessStatusCode();
        var paid = await Client.GetFromJsonAsync<JsonElement>($"/api/v1/invoices/{id}");
        Assert.Equal("Paid", paid.GetProperty("summary").GetProperty("status").GetString());

        // The customer's details are remembered for the next invoice.
        var (nextJob, _) = await DeliveredJobAsync("tinex  KUMANOVO");
        overview = await Client.GetFromJsonAsync<JsonElement>("/api/v1/invoices");
        var suggestion = overview.GetProperty("toInvoice").EnumerateArray().First(j => j.GetProperty("job").GetProperty("id").GetGuid() == nextJob);
        Assert.Equal("ap@tinex.example", suggestion.GetProperty("customerEmail").GetString());
    }

    [Fact]
    public async Task InvoicesIssuedAtTheSameMoment_GetUniqueConsecutiveNumbers()
    {
        await SaveProfileAsync("CC");
        var jobs = new List<Guid>();
        for (var i = 0; i < 8; i++)
        {
            jobs.Add((await DeliveredJobAsync($"Parallel {i}")).JobId);
        }

        var responses = await Task.WhenAll(jobs.Select(j => InvoiceAsync(j)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        var sequences = new List<int>();
        foreach (var r in responses)
        {
            var number = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("summary").GetProperty("number").GetString()!;
            sequences.Add(int.Parse(number.Split('-')[^1]));
        }

        sequences.Sort();
        Assert.Equal(Enumerable.Range(sequences[0], 8), sequences);
    }

    [Fact]
    public async Task EmailSending_WhenNoMailServerIsConfigured_IsRefusedWithAClearReason()
    {
        await SaveProfileAsync("EM");
        var profile = await Client.GetFromJsonAsync<JsonElement>("/api/v1/billing/profile");
        Assert.False(profile.GetProperty("emailSendingEnabled").GetBoolean());

        var (jobId, _) = await DeliveredJobAsync("No mail");
        var created = await (await InvoiceAsync(jobId)).Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("summary").GetProperty("id").GetGuid();

        var send = await Client.PostAsJsonAsync($"/api/v1/invoices/{id}/send", new { email = true });
        Assert.Equal(HttpStatusCode.Conflict, send.StatusCode);
        Assert.Contains("Copy the invoice link", await send.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Profile_WithAnInvalidBankAccount_IsRejected()
    {
        var response = await Client.PutAsJsonAsync("/api/v1/billing/profile", new
        {
            legalName = "X", address = "Y", iban = "12", currency = "EUR", defaultVatRatePercent = 18, defaultPaymentTermsDays = 30, invoicePrefix = "INV",
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
