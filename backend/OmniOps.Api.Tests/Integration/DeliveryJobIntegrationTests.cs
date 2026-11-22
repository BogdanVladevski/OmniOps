using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace OmniOps.Api.Tests.Integration;

[Collection(OmniOpsApiCollection.Name)]
public class DeliveryJobIntegrationTests(OmniOpsApiFixture fixture)
{
    private HttpClient Client => fixture.Client;

    private async Task<JsonElement> CreateJobAsync(string vehicleLabel = "Truck-002")
    {
        var assignable = await Client.GetFromJsonAsync<JsonElement>("/api/v1/jobs/assignable");
        var vehicle = assignable.GetProperty("vehicles").EnumerateArray()
            .First(v => v.GetProperty("label").GetString() == vehicleLabel);

        var response = await Client.PostAsJsonAsync("/api/v1/jobs", new
        {
            vehicleId = vehicle.GetProperty("id").GetGuid(),
            customerName = "Apteka Zegin",
            reference = "PO-1042",
            origin = "Skopje depot",
            destination = "Bitola",
            destinationLatitude = 41.0297,
            destinationLongitude = 21.3292,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static object Points(params (double Lat, double Lon, DateTime At)[] points) => new
    {
        points = points.Select(p => new
        {
            id = Guid.NewGuid(),
            latitude = p.Lat,
            longitude = p.Lon,
            speedKmh = 72.0,
            heading = 200.0,
            accuracyMeters = 6.0,
            recordedAtUtc = p.At,
        }).ToArray(),
    };

    [Fact]
    public async Task FullDelivery_FromCreationToProofToCustomerLink()
    {
        var job = await CreateJobAsync();
        var jobId = job.GetProperty("id").GetGuid();
        var trackingPath = job.GetProperty("trackingPath").GetString()!;
        var token = trackingPath["/t/".Length..];

        // Before the truck leaves, the customer sees "Planned" and no position.
        var planned = await Client.GetFromJsonAsync<JsonElement>($"/api/public/track/{token}");
        Assert.Equal("Planned", planned.GetProperty("status").GetString());

        // GPS before start is refused with a readable reason.
        var early = await Client.PostAsJsonAsync($"/api/v1/driver/jobs/{jobId}/locations", Points((41.9, 21.4, DateTime.UtcNow)));
        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);

        (await Client.PostAsync($"/api/v1/driver/jobs/{jobId}/start", null)).EnsureSuccessStatusCode();

        var batch = Points((41.80, 21.40, DateTime.UtcNow.AddMinutes(-1)), (41.70, 21.38, DateTime.UtcNow));
        var sent = await Client.PostAsJsonAsync($"/api/v1/driver/jobs/{jobId}/locations", batch);
        sent.EnsureSuccessStatusCode();
        Assert.Equal(2, (await sent.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accepted").GetInt32());

        // Resending the same batch (lost response, app restart) stores nothing twice.
        var resent = await Client.PostAsJsonAsync($"/api/v1/driver/jobs/{jobId}/locations", batch);
        Assert.Equal(0, (await resent.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accepted").GetInt32());

        var onTheWay = await Client.GetFromJsonAsync<JsonElement>($"/api/public/track/{token}");
        Assert.Equal("OnTheWay", onTheWay.GetProperty("status").GetString());
        Assert.Equal(41.70, onTheWay.GetProperty("latitude").GetDouble(), 3);
        Assert.Equal(JsonValueKind.String, onTheWay.GetProperty("etaUtc").ValueKind);

        // Delivery needs proof.
        var noProof = await Client.PostAsJsonAsync($"/api/v1/driver/jobs/{jobId}/deliver", new { recipientName = "Ana" });
        Assert.Equal(HttpStatusCode.Conflict, noProof.StatusCode);

        using var form = new MultipartFormDataContent();
        var image = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4]);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(image, "file", "pod.jpg");
        var upload = await Client.PostAsync($"/api/v1/driver/jobs/{jobId}/photos?latitude=41.03&longitude=21.33", form);
        upload.EnsureSuccessStatusCode();
        var photoId = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var delivered = await Client.PostAsJsonAsync($"/api/v1/driver/jobs/{jobId}/deliver", new { recipientName = "Ana Petrovska" });
        delivered.EnsureSuccessStatusCode();

        var detail = await Client.GetFromJsonAsync<JsonElement>($"/api/v1/jobs/{jobId}");
        Assert.Equal("Completed", detail.GetProperty("job").GetProperty("status").GetString());
        Assert.Equal(1, detail.GetProperty("job").GetProperty("photoCount").GetInt32());

        var bytes = await Client.GetByteArrayAsync($"/api/v1/jobs/{jobId}/documents/{photoId}");
        Assert.Equal([0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4], bytes);

        var done = await Client.GetFromJsonAsync<JsonElement>($"/api/public/track/{token}");
        Assert.Equal("Delivered", done.GetProperty("status").GetString());
        Assert.Equal("Ana Petrovska", done.GetProperty("recipientName").GetString());
        Assert.Equal(JsonValueKind.Null, done.GetProperty("latitude").ValueKind);
    }

    [Fact]
    public async Task PhoneGps_OnTruckCarryingColdChainShipment_RaisesNoTemperatureIncident()
    {
        // Truck-001 carries a seeded cold-chain shipment. Phone points have no cargo reading and must not
        // be read as 0 °C (which would be a false critical excursion).
        var job = await CreateJobAsync("Truck-001");
        var jobId = job.GetProperty("id").GetGuid();
        (await Client.PostAsync($"/api/v1/driver/jobs/{jobId}/start", null)).EnsureSuccessStatusCode();
        var since = DateTime.UtcNow;

        (await Client.PostAsJsonAsync($"/api/v1/driver/jobs/{jobId}/locations",
            Points((41.99, 21.43, DateTime.UtcNow)))).EnsureSuccessStatusCode();

        var incidents = await Client.GetFromJsonAsync<JsonElement[]>(
            "/api/incidents?fleetId=f1000000-0000-0000-0000-000000000001");
        Assert.DoesNotContain(incidents!, i =>
            i.GetProperty("vehicleId").GetString() == "Truck-001"
            && i.GetProperty("type").GetString() == "TemperatureExcursion"
            && i.GetProperty("detectedAtUtc").GetDateTime() >= since.AddMinutes(-1));
    }

    [Fact]
    public async Task Locations_WithTimeZoneOffset_AreAccepted()
    {
        // Regression: phones that send local time with an offset got HTTP 500.
        var job = await CreateJobAsync();
        var jobId = job.GetProperty("id").GetGuid();
        (await Client.PostAsync($"/api/v1/driver/jobs/{jobId}/start", null)).EnsureSuccessStatusCode();
        var local = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(2)).ToString("yyyy-MM-ddTHH:mm:ss.fffzzz");

        var response = await Client.PostAsync($"/api/v1/driver/jobs/{jobId}/locations", new StringContent(
            $$"""{"points":[{"id":"{{Guid.NewGuid()}}","latitude":41.9,"longitude":21.4,"recordedAtUtc":"{{local}}"}]}""",
            System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RefrigeratedLoad_TemperatureLogGatesDelivery()
    {
        var assignable = await Client.GetFromJsonAsync<JsonElement>("/api/v1/jobs/assignable");
        var vehicleId = assignable.GetProperty("vehicles")[0].GetProperty("id").GetGuid();
        var created = await Client.PostAsJsonAsync("/api/v1/jobs", new
        {
            vehicleId, customerName = "Cold store", destination = "Bitola", minTempCelsius = 2.0, maxTempCelsius = 8.0,
        });
        var jobId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // Loading note before the trip starts.
        (await Upload(jobId, "PickupDocument")).EnsureSuccessStatusCode();
        (await Client.PostAsync($"/api/v1/driver/jobs/{jobId}/start", null)).EnsureSuccessStatusCode();
        (await Upload(jobId, "DeliveryPhoto")).EnsureSuccessStatusCode();

        // A reading is required at the door.
        var noReading = await Client.PostAsJsonAsync($"/api/v1/driver/jobs/{jobId}/deliver", new { recipientName = "Ana" });
        Assert.Equal(HttpStatusCode.Conflict, noReading.StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(jobId, "TemperaturePhoto")).StatusCode); // no value
        (await Upload(jobId, "TemperaturePhoto", "&temperatureCelsius=9.5")).EnsureSuccessStatusCode();
        (await Client.PostAsJsonAsync($"/api/v1/driver/jobs/{jobId}/deliver", new { recipientName = "Ana" })).EnsureSuccessStatusCode();

        var detail = await Client.GetFromJsonAsync<JsonElement>($"/api/v1/jobs/{jobId}");
        var job = detail.GetProperty("job");
        Assert.Equal(1, job.GetProperty("pickupDocumentCount").GetInt32());
        Assert.Equal(1, job.GetProperty("temperatureOutOfRangeCount").GetInt32());
        Assert.Contains(detail.GetProperty("documents").EnumerateArray(),
            d => d.GetProperty("kind").GetString() == "TemperaturePhoto" && d.GetProperty("temperatureCelsius").GetDouble() == 9.5);

        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(jobId, "Invoice")).StatusCode);
    }

    private async Task<HttpResponseMessage> Upload(Guid jobId, string kind, string extra = "", string? note = null)
    {
        using var form = new MultipartFormDataContent();
        var image = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0, 9]);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(image, "file", "photo.jpg");
        if (note is not null)
        {
            form.Add(new StringContent(note), "note");
        }

        return await Client.PostAsync($"/api/v1/driver/jobs/{jobId}/documents?kind={kind}{extra}", form);
    }

    private static string At(DateTime utc) => Uri.EscapeDataString(new DateTimeOffset(utc).ToString("O"));

    [Fact]
    public async Task ADeliveryMadeWithoutSignal_IsRecordedOnce_WithTheTimesFromThePhone()
    {
        var created = await CreateJobAsync();
        var jobId = created.GetProperty("id").GetGuid();
        var createdAt = created.GetProperty("createdAtUtc").GetDateTime();
        await Task.Delay(1500); // the phone was offline: everything below happened before it reached the server
        var startedAt = createdAt.AddMilliseconds(300);
        var photoAt = createdAt.AddMilliseconds(800);
        var deliveredAt = createdAt.AddMilliseconds(1000);

        // The phone sends its queue when signal returns — and, after a dropped reply, sends it again.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            (await Client.PostAsJsonAsync($"/api/v1/driver/jobs/{jobId}/start", new { startedAt })).EnsureSuccessStatusCode();
        }

        var photoId = Guid.NewGuid();
        var first = await (await Upload(jobId, "DeliveryPhoto", $"&clientId={photoId}&capturedAt={At(photoAt)}")).Content.ReadFromJsonAsync<JsonElement>();
        var again = await (await Upload(jobId, "DeliveryPhoto", $"&clientId={photoId}&capturedAt={At(photoAt)}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(first.GetProperty("id").GetGuid(), again.GetProperty("id").GetGuid());

        for (var attempt = 0; attempt < 2; attempt++)
        {
            (await Client.PostAsJsonAsync($"/api/v1/driver/jobs/{jobId}/deliver",
                new { recipientName = "Vesna", deliveredAt })).EnsureSuccessStatusCode();
        }

        var detail = await Client.GetFromJsonAsync<JsonElement>($"/api/v1/jobs/{jobId}");
        var job = detail.GetProperty("job");
        Assert.Equal(1, job.GetProperty("photoCount").GetInt32());
        Assert.Equal(startedAt, job.GetProperty("startedAtUtc").GetDateTime(), TimeSpan.FromMilliseconds(5));
        Assert.Equal(deliveredAt, job.GetProperty("deliveredAtUtc").GetDateTime(), TimeSpan.FromMilliseconds(5));
        var photo = Assert.Single(detail.GetProperty("documents").EnumerateArray());
        Assert.Equal(photoAt, photo.GetProperty("capturedAtUtc").GetDateTime(), TimeSpan.FromMilliseconds(5));
    }

    [Fact]
    public async Task APhoneClockThatIsWrong_CannotPutAStartBeforeTheJobExisted()
    {
        var created = await CreateJobAsync();
        var jobId = created.GetProperty("id").GetGuid();
        (await Client.PostAsJsonAsync($"/api/v1/driver/jobs/{jobId}/start", new { startedAt = DateTime.UtcNow.AddYears(-1) })).EnsureSuccessStatusCode();
        var job = (await Client.GetFromJsonAsync<JsonElement>($"/api/v1/jobs/{jobId}")).GetProperty("job");
        Assert.Equal(created.GetProperty("createdAtUtc").GetDateTime(), job.GetProperty("startedAtUtc").GetDateTime(), TimeSpan.FromMilliseconds(5));
    }

    [Fact]
    public async Task ReceiverSignature_IsProofOfDelivery_AndProblemsAreRecorded()
    {
        var jobId = (await CreateJobAsync()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict, (await Upload(jobId, "Signature", note: "Vesna")).StatusCode); // not started

        // Damage found at loading.
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(jobId, "Exception")).StatusCode);
        (await Upload(jobId, "Exception", note: "2 cartons crushed")).EnsureSuccessStatusCode();

        (await Client.PostAsync($"/api/v1/driver/jobs/{jobId}/start", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(jobId, "Signature")).StatusCode); // who signed?
        (await Upload(jobId, "Signature", note: "Vesna Trajkova")).EnsureSuccessStatusCode();
        (await Client.PostAsJsonAsync($"/api/v1/driver/jobs/{jobId}/deliver", new { recipientName = "Vesna Trajkova" })).EnsureSuccessStatusCode();

        var detail = await Client.GetFromJsonAsync<JsonElement>($"/api/v1/jobs/{jobId}");
        Assert.Equal(1, detail.GetProperty("job").GetProperty("signatureCount").GetInt32());
        Assert.Equal(1, detail.GetProperty("job").GetProperty("exceptionCount").GetInt32());
        Assert.Contains(detail.GetProperty("documents").EnumerateArray(),
            d => d.GetProperty("kind").GetString() == "Exception" && d.GetProperty("note").GetString() == "2 cartons crushed");
    }

    [Fact]
    public async Task Delivery_NeedsAPhotoOrASignature()
    {
        var jobId = (await CreateJobAsync()).GetProperty("id").GetGuid();
        (await Client.PostAsync($"/api/v1/driver/jobs/{jobId}/start", null)).EnsureSuccessStatusCode();
        var refused = await Client.PostAsJsonAsync($"/api/v1/driver/jobs/{jobId}/deliver", new { recipientName = "Vesna" });
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Contains("signature", await refused.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task InvalidRequests_AreRejectedWithReasons()
    {
        var noDestination = await Client.PostAsJsonAsync("/api/v1/jobs", new { vehicleId = Guid.NewGuid(), customerName = "X" });
        Assert.Equal(HttpStatusCode.BadRequest, noDestination.StatusCode);

        var unknownVehicle = await Client.PostAsJsonAsync("/api/v1/jobs",
            new { vehicleId = Guid.NewGuid(), customerName = "X", destination = "Y" });
        Assert.Equal(HttpStatusCode.NotFound, unknownVehicle.StatusCode);

        var unknownLink = await Client.GetAsync("/api/public/track/AAAAAAAAAAAAAAAAAAAAAA");
        Assert.Equal(HttpStatusCode.NotFound, unknownLink.StatusCode);
    }

    [Fact]
    public async Task TrackingPage_IsServedWithoutLogin()
    {
        var page = await Client.GetAsync("/t/AAAAAAAAAAAAAAAAAAAAAA");
        page.EnsureSuccessStatusCode();
        Assert.Equal("text/html", page.Content.Headers.ContentType?.MediaType);
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains("/api/public/track/", html);
        // Regression: "no-referrer" made OpenStreetMap block every map tile. Origin-only keeps the token private.
        Assert.Contains("content=\"strict-origin-when-cross-origin\"", html);
        Assert.DoesNotContain("content=\"no-referrer\"", html);
    }
}
