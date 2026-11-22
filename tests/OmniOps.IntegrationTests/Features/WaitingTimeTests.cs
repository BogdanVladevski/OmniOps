using Microsoft.EntityFrameworkCore;
using OmniOps.Application.Features.Jobs;
namespace OmniOps.IntegrationTests.Features;

/// <summary>Waiting time at pickup and delivery: recorded from the phone, priced by the company's terms, invoiced.</summary>
[Collection(ApiTestGroup.Name)]
public class WaitingTimeTests(ApiFixture fixture)
{
    private const double PickupLat = 41.9981, PickupLon = 21.4254;   // Skopje
    private const double DropLat = 41.0297, DropLon = 21.3292;       // Bitola

    private static double North(double lat, double meters) => lat + meters / 111_320.0;

    private async Task<(HttpClient Owner, Guid JobId)> CompanyWithJobAsync(decimal? rate)
    {
        var company = await fixture.NewCompanyAsync("Waiting Haulage");
        (await company.SaveBillingProfileAsync("WH", rate, freeWaitingMinutes: 60)).EnsureSuccessStatusCode();
        var job = await company.CreateJobAsync(new
        {
            customerName = "Bitola Market", origin = "Skopje depot", originLatitude = PickupLat, originLongitude = PickupLon,
            destination = "Bitola", destinationLatitude = DropLat, destinationLongitude = DropLon,
        });
        var jobId = job.GetProperty("id").GetGuid();
        await fixture.BackdateJobAsync(jobId, TimeSpan.FromHours(8)); // planned this morning
        return (company.Owner, jobId);
    }

    private static object Point(DateTime at, double lat, double lon) =>
        new { id = Guid.NewGuid(), latitude = lat, longitude = lon, speedKmh = 0, recordedAtUtc = at };

    [Fact]
    public async Task WaitsAreRecordedFromThePhone_PricedPastTheFreeTime_AndSuggestedOnTheInvoice()
    {
        var (owner, jobId) = await CompanyWithJobAsync(rate: 40m);
        var now = DateTime.UtcNow;

        // Pickup: the driver taps "I've arrived" at the depot, waits 2 h for loading, then starts the trip.
        (await owner.PostAsJsonAsync($"/api/v1/driver/jobs/{jobId}/arrived", new
        {
            stop = "pickup", arrivedAt = now.AddHours(-5), latitude = North(PickupLat, 60), longitude = PickupLon,
        })).EnsureSuccessStatusCode();
        (await owner.PostAsJsonAsync($"/api/v1/driver/jobs/{jobId}/start", new { startedAt = now.AddHours(-3) })).EnsureSuccessStatusCode();

        // Delivery: GPS shows the truck reaching the customer's yard 90 minutes ago.
        (await owner.PostAsJsonAsync($"/api/v1/driver/jobs/{jobId}/locations", new
        {
            points = new[]
            {
                Point(now.AddMinutes(-120), North(DropLat, 20_000), DropLon),
                Point(now.AddMinutes(-90), North(DropLat, 120), DropLon),
                Point(now.AddMinutes(-60), North(DropLat, 40), DropLon),
            },
        })).EnsureSuccessStatusCode();

        var detail = await owner.GetFromJsonAsync<JsonElement>($"/api/v1/jobs/{jobId}");
        var waits = detail.GetProperty("waits").EnumerateArray().ToList();
        var pickup = waits.Single(w => w.GetProperty("stop").GetString() == "pickup");
        Assert.Equal(120, pickup.GetProperty("waitedMinutes").GetInt32());
        Assert.Equal(60, pickup.GetProperty("chargedMinutes").GetInt32());
        Assert.Equal(40m, pickup.GetProperty("amount").GetDecimal());
        Assert.True(pickup.GetProperty("verified").GetBoolean());
        var delivery = waits.Single(w => w.GetProperty("stop").GetString() == "delivery");
        Assert.Equal("Gps", delivery.GetProperty("source").GetString());
        Assert.True(delivery.GetProperty("running").GetBoolean());
        Assert.Equal(now.AddMinutes(-90), delivery.GetProperty("arrivedAtUtc").GetDateTime(), TimeSpan.FromSeconds(1));

        // The receiver's tracking page says when the truck arrived and from when waiting is charged.
        var tracking = await fixture.Anonymous().GetFromJsonAsync<JsonElement>(
            "/api/public/track/" + detail.GetProperty("job").GetProperty("trackingPath").GetString()!.Split('/').Last());
        Assert.Equal(now.AddMinutes(-90), tracking.GetProperty("arrivedAtUtc").GetDateTime(), TimeSpan.FromSeconds(1));
        Assert.Equal(now.AddMinutes(-30), tracking.GetProperty("waitingChargedFromUtc").GetDateTime(), TimeSpan.FromSeconds(1));

        // The office is told once that free time is over.
        await RunWaitingAlertsAsync();
        var alertedAt = await AlertedAtAsync(jobId);
        Assert.NotNull(alertedAt);
        await RunWaitingAlertsAsync();
        Assert.Equal(alertedAt, await AlertedAtAsync(jobId));

        // Delivered 10 minutes ago: 80 minutes at the customer, 60 free, 20 over → 30 charged (started quarter hours).
        (await Company.UploadAsync(owner, jobId, "DeliveryPhoto")).EnsureSuccessStatusCode();

        (await owner.PostAsJsonAsync($"/api/v1/driver/jobs/{jobId}/deliver",
            new { recipientName = "Ana", deliveredAt = now.AddMinutes(-10) })).EnsureSuccessStatusCode();

        var overview = await owner.GetFromJsonAsync<JsonElement>("/api/v1/invoices");
        var item = overview.GetProperty("toInvoice").EnumerateArray().Single(i => i.GetProperty("job").GetProperty("id").GetGuid() == jobId);
        var lines = item.GetProperty("suggestedLines").EnumerateArray().ToList();
        Assert.Equal(2, lines.Count);
        Assert.Equal(1m, lines[0].GetProperty("quantity").GetDecimal());
        Assert.Equal(40m, lines[0].GetProperty("unitPrice").GetDecimal());
        Assert.StartsWith("Waiting time at loading, Skopje depot", lines[0].GetProperty("description").GetString());
        Assert.Equal(0.5m, lines[1].GetProperty("quantity").GetDecimal());
        Assert.Contains("unloading, Bitola", lines[1].GetProperty("description").GetString());

        // The suggested lines go onto the invoice like any other line, and the invoice page shows the stop times.
        var created = await owner.PostAsJsonAsync("/api/v1/invoices", new
        {
            jobId, customerName = "Bitola Market", paymentTermsDays = 30,
            lines = new object[]
            {
                new { description = "Transport Skopje → Bitola", quantity = 1, unitPrice = 300, vatRatePercent = 18 },
                new { description = lines[0].GetProperty("description").GetString(), quantity = 1, unitPrice = 40, vatRatePercent = 18 },
                new { description = lines[1].GetProperty("description").GetString(), quantity = 0.5, unitPrice = 40, vatRatePercent = 18 },
            },
        });
        created.EnsureSuccessStatusCode();
        var invoice = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(360m, invoice.GetProperty("subtotal").GetDecimal());
        var token = invoice.GetProperty("summary").GetProperty("publicPath").GetString()!.Split('/').Last();
        var page = await fixture.Anonymous().GetFromJsonAsync<JsonElement>($"/api/public/invoices/{token}");
        Assert.Equal(2, page.GetProperty("stopTimes").GetArrayLength());
    }

    [Fact]
    public async Task GpsReadingsFromBeforeTheTripStarted_AreNotAnArrival()
    {
        var (owner, jobId) = await CompanyWithJobAsync(rate: 40m);
        var now = DateTime.UtcNow;
        (await owner.PostAsJsonAsync($"/api/v1/driver/jobs/{jobId}/start", new { startedAt = now.AddHours(-1) })).EnsureSuccessStatusCode();
        (await owner.PostAsJsonAsync($"/api/v1/driver/jobs/{jobId}/locations", new
        {
            points = new[] { Point(now.AddHours(-3), North(DropLat, 50), DropLon), Point(now.AddMinutes(-90), North(DropLat, 60), DropLon) },
        })).EnsureSuccessStatusCode();
        Assert.Empty((await owner.GetFromJsonAsync<JsonElement>($"/api/v1/jobs/{jobId}")).GetProperty("waits").EnumerateArray());

        (await owner.PostAsJsonAsync($"/api/v1/driver/jobs/{jobId}/locations", new
        {
            points = new[] { Point(now.AddMinutes(-20), North(DropLat, 50), DropLon) },
        })).EnsureSuccessStatusCode();
        var wait = (await owner.GetFromJsonAsync<JsonElement>($"/api/v1/jobs/{jobId}")).GetProperty("waits")[0];
        Assert.Equal(now.AddMinutes(-20), wait.GetProperty("arrivedAtUtc").GetDateTime(), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task WithoutARate_WaitingIsRecordedButNothingIsCharged()
    {
        var (owner, jobId) = await CompanyWithJobAsync(rate: null);
        (await owner.PostAsJsonAsync($"/api/v1/driver/jobs/{jobId}/arrived",
            new { stop = "pickup", arrivedAt = DateTime.UtcNow.AddHours(-4) })).EnsureSuccessStatusCode();
        (await owner.PostAsJsonAsync($"/api/v1/driver/jobs/{jobId}/start", new { startedAt = DateTime.UtcNow.AddHours(-1) })).EnsureSuccessStatusCode();

        var wait = (await owner.GetFromJsonAsync<JsonElement>($"/api/v1/jobs/{jobId}")).GetProperty("waits")[0];
        Assert.Equal(180, wait.GetProperty("waitedMinutes").GetInt32());
        Assert.Equal(JsonValueKind.Null, wait.GetProperty("amount").ValueKind);
    }

    [Fact]
    public async Task AnotherCompany_CannotMarkAnArrival()
    {
        var (_, jobId) = await CompanyWithJobAsync(rate: 40m);
        var (other, _) = await CompanyWithJobAsync(rate: 40m);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/v1/driver/jobs/{jobId}/arrived",
            new { stop = "pickup" })).StatusCode);
    }

    private Task<int> RunWaitingAlertsAsync() => fixture.SendAsync(new ProcessWaitingAlertsCommand());

    private async Task<DateTime?> AlertedAtAsync(Guid jobId)
    {
        DateTime? at = null;
        await fixture.WithDatabaseAsync(async db =>
            at = await db.Jobs.IgnoreQueryFilters().Where(j => j.Id == jobId).Select(j => j.DeliveryWaitAlertedAtUtc).SingleAsync());
        return at;
    }
}
