using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OmniOps.Application.Commands;
using OmniOps.Infrastructure.Data;

namespace OmniOps.Api.Tests.Integration;

/// <summary>Waiting time at pickup and delivery: recorded from the phone, priced by the company's terms, invoiced.</summary>
[Collection(OmniOpsApiCollection.Name)]
public class WaitingTimeIntegrationTests(OmniOpsApiFixture fixture)
{
    private const double PickupLat = 41.9981, PickupLon = 21.4254;   // Skopje
    private const double DropLat = 41.0297, DropLon = 21.3292;       // Bitola

    private static double North(double lat, double meters) => lat + meters / 111_320.0;

    private async Task<(HttpClient Owner, Guid JobId)> CompanyWithJobAsync(decimal? rate)
    {
        var email = $"{Guid.NewGuid():N}@example.test";
        var signUp = await fixture.AnonymousClient().PostAsJsonAsync("/api/v1/account/sign-up",
            new { companyName = "Waiting Haulage", name = "Owner", email, password = "correct horse battery" });
        var session = await signUp.Content.ReadFromJsonAsync<JsonElement>();
        var owner = fixture.ClientFor(session.GetProperty("accessToken").GetString()!);
        var fleetId = session.GetProperty("account").GetProperty("defaultFleetId").GetGuid();

        (await owner.PutAsJsonAsync("/api/v1/billing/profile", new
        {
            legalName = "Waiting Haulage DOOEL", address = "Partizanska 1, Bitola", iban = "MK07250120000058984",
            currency = "EUR", defaultVatRatePercent = 18, defaultPaymentTermsDays = 30, invoicePrefix = "WH",
            freeWaitingMinutes = 60, waitingRatePerHour = rate, timeZone = "Europe/Skopje",
        })).EnsureSuccessStatusCode();

        var vehicle = await (await owner.PostAsJsonAsync("/api/vehicles", new { fleetId, externalId = "WH-1" })).Content.ReadFromJsonAsync<JsonElement>();
        var job = await owner.PostAsJsonAsync("/api/v1/jobs", new
        {
            vehicleId = vehicle.GetProperty("id").GetGuid(), customerName = "Bitola Market",
            origin = "Skopje depot", originLatitude = PickupLat, originLongitude = PickupLon,
            destination = "Bitola", destinationLatitude = DropLat, destinationLongitude = DropLon,
        });
        Assert.Equal(HttpStatusCode.Created, job.StatusCode);
        var jobId = (await job.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // The job was planned this morning.
        using var scope = fixture.Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Trips.Where(t => t.Id == jobId)
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.CreatedAtUtc, DateTime.UtcNow.AddHours(-8)));
        return (owner, jobId);
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
        var tracking = await fixture.AnonymousClient().GetFromJsonAsync<JsonElement>(
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
        using (var form = new MultipartFormDataContent())
        {
            var image = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0, 9]);
            image.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            form.Add(image, "file", "pod.jpg");
            (await owner.PostAsync($"/api/v1/driver/jobs/{jobId}/documents?kind=DeliveryPhoto", form)).EnsureSuccessStatusCode();
        }

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
        var page = await fixture.AnonymousClient().GetFromJsonAsync<JsonElement>($"/api/public/invoices/{token}");
        Assert.Equal(2, page.GetProperty("stopTimes").GetArrayLength());
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

    private async Task RunWaitingAlertsAsync()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new ProcessWaitingAlertsCommand());
    }

    private async Task<DateTime?> AlertedAtAsync(Guid jobId)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Trips
            .Where(t => t.Id == jobId).Select(t => t.DeliveryWaitAlertedAtUtc).SingleAsync();
    }
}
