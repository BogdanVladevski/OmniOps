using System.Net.Http.Headers;

namespace OmniOps.IntegrationTests.Infrastructure;

/// <summary>A company in the test database, with its owner's signed-in client and helpers for common setup.</summary>
public sealed class Company(ApiFixture api, HttpClient owner, JsonElement session, string email)
{
    public const string Password = "correct horse battery";

    public HttpClient Owner { get; } = owner;
    public JsonElement Session { get; } = session;
    public string Email { get; } = email;
    public Guid OrganizationId => Session.GetProperty("account").GetProperty("organizationId").GetGuid();
    public Guid OwnerId => Session.GetProperty("account").GetProperty("userId").GetGuid();

    public async Task<Guid> AddVehicleAsync(string name = "Truck-1") =>
        (await (await Owner.PostAsJsonAsync("/api/v1/vehicles", new { name })).ReadJsonAsync()).GetProperty("id").GetGuid();

    public async Task<Guid> AddDriverAsync(string fullName = "Marko", string? phone = null) =>
        (await (await Owner.PostAsJsonAsync("/api/v1/drivers", new { fullName, phone })).ReadJsonAsync()).GetProperty("id").GetGuid();

    /// <summary>A job to Bitola on a new truck. Extra fields override the defaults.</summary>
    public async Task<JsonElement> CreateJobAsync(object? fields = null, Guid? vehicleId = null, Guid? driverId = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["vehicleId"] = vehicleId ?? await AddVehicleAsync($"T-{Guid.NewGuid().ToString("N")[..6]}"),
            ["driverId"] = driverId,
            ["customerName"] = "Bitola Market",
            ["reference"] = "PO-1042",
            ["origin"] = "Skopje depot",
            ["destination"] = "Bitola",
            ["destinationLatitude"] = 41.0297,
            ["destinationLongitude"] = 21.3292,
        };
        foreach (var p in fields?.GetType().GetProperties() ?? [])
        {
            body[char.ToLowerInvariant(p.Name[0]) + p.Name[1..]] = p.GetValue(fields);
        }

        var response = await Owner.PostAsJsonAsync("/api/v1/jobs", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadJsonAsync();
    }

    /// <summary>Signs a driver's phone in with a one-time code; returns its client and session.</summary>
    public async Task<(HttpClient Phone, JsonElement Session)> PairDriverAsync(Guid driverId)
    {
        var code = (await (await Owner.PostAsJsonAsync("/api/v1/account/driver-codes", new { driverId })).ReadJsonAsync()).GetProperty("code").GetString();
        var session = await (await api.Anonymous().PostAsJsonAsync("/api/v1/account/pair", new { code })).ReadJsonAsync();
        return (api.SignedIn(session.GetProperty("accessToken").GetString()!), session);
    }

    /// <summary>Uploads a (tiny) photo as the given client. Extra form fields: temperatureCelsius, note, clientId, capturedAt…</summary>
    public static async Task<HttpResponseMessage> UploadAsync(HttpClient client, Guid jobId, string kind, IDictionary<string, string>? fields = null)
    {
        using var form = new MultipartFormDataContent();
        var image = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0, 9]);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(image, "file", "photo.jpg");
        form.Add(new StringContent(kind), "kind");
        foreach (var (name, value) in fields ?? new Dictionary<string, string>())
        {
            form.Add(new StringContent(value), name);
        }

        return await client.PostAsync($"/api/v1/driver/jobs/{jobId}/documents", form);
    }

    public Task<HttpResponseMessage> UploadAsync(Guid jobId, string kind, IDictionary<string, string>? fields = null) =>
        UploadAsync(Owner, jobId, kind, fields);

    /// <summary>Started, photographed and delivered.</summary>
    public async Task DeliverAsync(Guid jobId, string recipient = "Ana")
    {
        (await Owner.PostAsync($"/api/v1/driver/jobs/{jobId}/start", null)).EnsureSuccessStatusCode();
        (await UploadAsync(jobId, "DeliveryPhoto")).EnsureSuccessStatusCode();
        (await Owner.PostAsJsonAsync($"/api/v1/driver/jobs/{jobId}/deliver", new { recipientName = recipient })).EnsureSuccessStatusCode();
    }

    public Task<HttpResponseMessage> SaveBillingProfileAsync(string prefix = "INV", decimal? waitingRate = null, int freeWaitingMinutes = 120) =>
        Owner.PutAsJsonAsync("/api/v1/billing/profile", new
        {
            legalName = "Test Haulage DOOEL", address = "Partizanska 1, Bitola", iban = "MK07250120000058984", currency = "EUR",
            defaultVatRatePercent = 18, defaultPaymentTermsDays = 30, invoicePrefix = prefix,
            freeWaitingMinutes, waitingRatePerHour = waitingRate, timeZone = "Europe/Skopje",
        });
}

public static class HttpExtensions
{
    /// <summary>Asserts success (showing the problem details otherwise) and reads the JSON body.</summary>
    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static async Task<JsonElement> GetJsonAsync(this HttpClient client, string url) => await (await client.GetAsync(url)).ReadJsonAsync();
}
