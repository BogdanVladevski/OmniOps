namespace OmniOps.IntegrationTests.Features;

/// <summary>A delivery from creation to proof, the driver's phone (offline, resends) and the customer's tracking link.</summary>
[Collection(ApiTestGroup.Name)]
public class JobTests(ApiFixture fixture)
{
    private static object Points(params (double Lat, double Lon, DateTime At)[] points) => new
    {
        points = points.Select(p => new { id = Guid.NewGuid(), latitude = p.Lat, longitude = p.Lon, speedKmh = 72.0, accuracyMeters = 6.0, recordedAtUtc = p.At }).ToArray(),
    };

    private static Dictionary<string, string> Fields(params (string Name, object Value)[] fields) =>
        fields.ToDictionary(f => f.Name, f => Convert.ToString(f.Value, System.Globalization.CultureInfo.InvariantCulture)!);

    [Fact]
    public async Task FullDelivery_FromCreationToProofToCustomerLink()
    {
        var company = await fixture.NewCompanyAsync();
        var job = await company.CreateJobAsync();
        var jobId = job.GetProperty("id").GetGuid();
        var token = job.GetProperty("trackingPath").GetString()!["/t/".Length..];
        var customer = fixture.Anonymous();

        // Before the truck leaves, the customer sees "Planned" and no position.
        Assert.Equal("Planned", (await customer.GetJsonAsync($"/api/public/track/{token}")).GetProperty("status").GetString());

        // GPS before start is refused with a readable reason.
        Assert.Equal(HttpStatusCode.Conflict, (await company.Owner.PostAsJsonAsync($"/api/v1/driver/jobs/{jobId}/locations",
            Points((41.9, 21.4, DateTime.UtcNow)))).StatusCode);

        (await company.Owner.PostAsync($"/api/v1/driver/jobs/{jobId}/start", null)).EnsureSuccessStatusCode();
        var batch = Points((41.80, 21.40, DateTime.UtcNow.AddMinutes(-1)), (41.70, 21.38, DateTime.UtcNow));
        Assert.Equal(2, (await (await company.Owner.PostAsJsonAsync($"/api/v1/driver/jobs/{jobId}/locations", batch)).ReadJsonAsync())
            .GetProperty("accepted").GetInt32());

        // Resending the same batch (lost response, app restart) stores nothing twice.
        Assert.Equal(0, (await (await company.Owner.PostAsJsonAsync($"/api/v1/driver/jobs/{jobId}/locations", batch)).ReadJsonAsync())
            .GetProperty("accepted").GetInt32());

        var onTheWay = await customer.GetJsonAsync($"/api/public/track/{token}");
        Assert.Equal("OnTheWay", onTheWay.GetProperty("status").GetString());
        Assert.Equal(41.70, onTheWay.GetProperty("latitude").GetDouble(), 3);
        Assert.Equal(JsonValueKind.String, onTheWay.GetProperty("etaUtc").ValueKind);

        // Delivery needs proof.
        var noProof = await company.Owner.PostAsJsonAsync($"/api/v1/driver/jobs/{jobId}/deliver", new { recipientName = "Ana" });
        Assert.Equal(HttpStatusCode.Conflict, noProof.StatusCode);
        Assert.Contains("signature", await noProof.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var photoId = (await (await company.UploadAsync(jobId, "DeliveryPhoto", Fields(("latitude", 41.03), ("longitude", 21.33)))).ReadJsonAsync())
            .GetProperty("id").GetGuid();
        (await company.Owner.PostAsJsonAsync($"/api/v1/driver/jobs/{jobId}/deliver", new { recipientName = "Ana Petrovska" })).EnsureSuccessStatusCode();

        var detail = await company.Owner.GetJsonAsync($"/api/v1/jobs/{jobId}");
        Assert.Equal("Delivered", detail.GetProperty("job").GetProperty("status").GetString());
        Assert.Equal(1, detail.GetProperty("job").GetProperty("photoCount").GetInt32());
        Assert.Equal([0xFF, 0xD8, 0xFF, 0xE0, 9], await company.Owner.GetByteArrayAsync($"/api/v1/jobs/{jobId}/documents/{photoId}"));

        var done = await customer.GetJsonAsync($"/api/public/track/{token}");
        Assert.Equal("Delivered", done.GetProperty("status").GetString());
        Assert.Equal("Ana Petrovska", done.GetProperty("recipientName").GetString());
        Assert.Equal(JsonValueKind.Null, done.GetProperty("latitude").ValueKind);
    }

    [Fact]
    public async Task Locations_WithATimeZoneOffset_AreStoredAsTheSameInstant()
    {
        // Regression: phones sending local time with an offset got HTTP 500.
        var company = await fixture.NewCompanyAsync();
        var jobId = (await company.CreateJobAsync()).GetProperty("id").GetGuid();
        (await company.Owner.PostAsync($"/api/v1/driver/jobs/{jobId}/start", null)).EnsureSuccessStatusCode();
        var local = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(2));

        var response = await company.Owner.PostAsync($"/api/v1/driver/jobs/{jobId}/locations", new StringContent(
            $$"""{"points":[{"id":"{{Guid.NewGuid()}}","latitude":41.9,"longitude":21.4,"recordedAtUtc":"{{local:yyyy-MM-ddTHH:mm:ss.fffzzz}}"}]}""",
            System.Text.Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();
        var at = (await company.Owner.GetJsonAsync($"/api/v1/jobs/{jobId}")).GetProperty("job").GetProperty("lastPositionAtUtc").GetDateTime();
        Assert.Equal(local.UtcDateTime, at, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task RefrigeratedLoad_TemperatureLogGatesDelivery()
    {
        var company = await fixture.NewCompanyAsync();
        var jobId = (await company.CreateJobAsync(new { minTempCelsius = 2.0, maxTempCelsius = 8.0 })).GetProperty("id").GetGuid();

        // Loading note before the job starts.
        (await company.UploadAsync(jobId, "PickupDocument")).EnsureSuccessStatusCode();
        (await company.Owner.PostAsync($"/api/v1/driver/jobs/{jobId}/start", null)).EnsureSuccessStatusCode();
        (await company.UploadAsync(jobId, "DeliveryPhoto")).EnsureSuccessStatusCode();

        // A reading is required at the door.
        Assert.Equal(HttpStatusCode.Conflict, (await company.Owner.PostAsJsonAsync($"/api/v1/driver/jobs/{jobId}/deliver", new { recipientName = "Ana" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await company.UploadAsync(jobId, "TemperaturePhoto")).StatusCode); // no value
        (await company.UploadAsync(jobId, "TemperaturePhoto", Fields(("temperatureCelsius", 9.5)))).EnsureSuccessStatusCode();
        (await company.Owner.PostAsJsonAsync($"/api/v1/driver/jobs/{jobId}/deliver", new { recipientName = "Ana" })).EnsureSuccessStatusCode();

        var detail = await company.Owner.GetJsonAsync($"/api/v1/jobs/{jobId}");
        var job = detail.GetProperty("job");
        Assert.Equal(1, job.GetProperty("pickupDocumentCount").GetInt32());
        Assert.Equal(1, job.GetProperty("temperatureOutOfRangeCount").GetInt32());
        Assert.Contains(detail.GetProperty("documents").EnumerateArray(),
            d => d.GetProperty("kind").GetString() == "TemperaturePhoto" && d.GetProperty("temperatureCelsius").GetDouble() == 9.5);
        Assert.Equal(HttpStatusCode.BadRequest, (await company.UploadAsync(jobId, "Invoice")).StatusCode);
    }

    [Fact]
    public async Task ADeliveryMadeWithoutSignal_IsRecordedOnce_WithTheTimesFromThePhone()
    {
        var company = await fixture.NewCompanyAsync();
        var jobId = (await company.CreateJobAsync()).GetProperty("id").GetGuid();
        await fixture.BackdateJobAsync(jobId, TimeSpan.FromHours(2));
        var now = DateTime.UtcNow;
        var (startedAt, photoAt, deliveredAt) = (now.AddMinutes(-50), now.AddMinutes(-12), now.AddMinutes(-10));

        // The phone sends its queue when signal returns, and after a dropped reply sends it again.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            (await company.Owner.PostAsJsonAsync($"/api/v1/driver/jobs/{jobId}/start", new { startedAt })).EnsureSuccessStatusCode();
        }

        var photo = Fields(("clientId", Guid.NewGuid()), ("capturedAt", photoAt.ToString("O")));
        var first = await (await company.UploadAsync(jobId, "DeliveryPhoto", photo)).ReadJsonAsync();
        var again = await (await company.UploadAsync(jobId, "DeliveryPhoto", photo)).ReadJsonAsync();
        Assert.Equal(first.GetProperty("id").GetGuid(), again.GetProperty("id").GetGuid());

        for (var attempt = 0; attempt < 2; attempt++)
        {
            (await company.Owner.PostAsJsonAsync($"/api/v1/driver/jobs/{jobId}/deliver", new { recipientName = "Vesna", deliveredAt })).EnsureSuccessStatusCode();
        }

        var detail = await company.Owner.GetJsonAsync($"/api/v1/jobs/{jobId}");
        var job = detail.GetProperty("job");
        Assert.Equal(1, job.GetProperty("photoCount").GetInt32());
        Assert.Equal(startedAt, job.GetProperty("startedAtUtc").GetDateTime(), TimeSpan.FromMilliseconds(5));
        Assert.Equal(deliveredAt, job.GetProperty("deliveredAtUtc").GetDateTime(), TimeSpan.FromMilliseconds(5));
        Assert.Equal(photoAt, Assert.Single(detail.GetProperty("documents").EnumerateArray()).GetProperty("capturedAtUtc").GetDateTime(), TimeSpan.FromMilliseconds(5));
    }

    [Fact]
    public async Task APhoneClockThatIsWrong_CannotPutAStartBeforeTheJobExisted()
    {
        var company = await fixture.NewCompanyAsync();
        var created = await company.CreateJobAsync();
        var jobId = created.GetProperty("id").GetGuid();
        (await company.Owner.PostAsJsonAsync($"/api/v1/driver/jobs/{jobId}/start", new { startedAt = DateTime.UtcNow.AddYears(-1) })).EnsureSuccessStatusCode();
        var job = (await company.Owner.GetJsonAsync($"/api/v1/jobs/{jobId}")).GetProperty("job");
        Assert.Equal(created.GetProperty("createdAtUtc").GetDateTime(), job.GetProperty("startedAtUtc").GetDateTime(), TimeSpan.FromMilliseconds(5));
    }

    [Fact]
    public async Task ReceiverSignature_IsProofOfDelivery_AndProblemsAreRecorded()
    {
        var company = await fixture.NewCompanyAsync();
        var jobId = (await company.CreateJobAsync()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict, (await company.UploadAsync(jobId, "Signature", Fields(("note", "Vesna")))).StatusCode); // not started

        // Damage found at loading.
        Assert.Equal(HttpStatusCode.BadRequest, (await company.UploadAsync(jobId, "Exception")).StatusCode);
        (await company.UploadAsync(jobId, "Exception", Fields(("note", "2 cartons crushed")))).EnsureSuccessStatusCode();

        (await company.Owner.PostAsync($"/api/v1/driver/jobs/{jobId}/start", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await company.UploadAsync(jobId, "Signature")).StatusCode); // who signed?
        (await company.UploadAsync(jobId, "Signature", Fields(("note", "Vesna Trajkova")))).EnsureSuccessStatusCode();
        (await company.Owner.PostAsJsonAsync($"/api/v1/driver/jobs/{jobId}/deliver", new { recipientName = "Vesna Trajkova" })).EnsureSuccessStatusCode();

        var detail = await company.Owner.GetJsonAsync($"/api/v1/jobs/{jobId}");
        Assert.Equal(1, detail.GetProperty("job").GetProperty("signatureCount").GetInt32());
        Assert.Equal(1, detail.GetProperty("job").GetProperty("exceptionCount").GetInt32());
        Assert.Contains(detail.GetProperty("documents").EnumerateArray(),
            d => d.GetProperty("kind").GetString() == "Exception" && d.GetProperty("note").GetString() == "2 cartons crushed");
    }

    [Fact]
    public async Task InvalidRequests_AreRejectedWithReasons()
    {
        var company = await fixture.NewCompanyAsync();
        var noDestination = await company.Owner.PostAsJsonAsync("/api/v1/jobs", new { vehicleId = Guid.NewGuid(), customerName = "X" });
        Assert.Equal(HttpStatusCode.BadRequest, noDestination.StatusCode);
        Assert.Contains("Where does it go?", await noDestination.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.NotFound, (await company.Owner.PostAsJsonAsync("/api/v1/jobs",
            new { vehicleId = Guid.NewGuid(), customerName = "X", destination = "Y" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Anonymous().GetAsync("/api/public/track/AAAAAAAAAAAAAAAAAAAAAA")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await company.Owner.PostAsJsonAsync("/api/v1/vehicles", new { name = "Dup" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await company.Owner.PostAsJsonAsync("/api/v1/vehicles", new { name = "Dup" })).StatusCode);
    }

    [Fact]
    public async Task PublicPages_KeepTheirTokenOutOfReferrers()
    {
        // Regression: "no-referrer" made OpenStreetMap block every map tile. Origin-only keeps the token private.
        var html = await fixture.Anonymous().GetStringAsync("/t/AAAAAAAAAAAAAAAAAAAAAA");
        Assert.Contains("strict-origin-when-cross-origin", html, StringComparison.Ordinal);
    }
}
