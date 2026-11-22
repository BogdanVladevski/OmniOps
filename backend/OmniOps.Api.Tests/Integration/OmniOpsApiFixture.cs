using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace OmniOps.Api.Tests.Integration;

public sealed class OmniOpsApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public HttpClient Client { get; private set; } = null!;

    /// <summary>Uploaded files go to a throwaway folder, never into the source tree.</summary>
    public string StorageRoot { get; } = Path.Combine(Path.GetTempPath(), "omniops-it-" + Guid.NewGuid().ToString("N"));

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Database:ConnectionString"] = _postgres.GetConnectionString(),
                        // Tests run the way production does: everything behind sign-in.
                        ["Jwt:RequireAuthentication"] = "true",
                        ["AccountRateLimit:PermitLimit"] = "10000",
                        ["Jwt:Secret"] = "integration-test-secret-at-least-32-chars",
                        ["Fleet:VehicleIds"] = "Truck-001,Truck-002,Truck-003",
                        ["Storage:RootPath"] = StorageRoot,
                    });
                });

                builder.ConfigureServices(services =>
                {
                    var hosted = services
                        .Where(d => d.ServiceType == typeof(IHostedService))
                        .ToList();
                    foreach (var descriptor in hosted)
                    {
                        services.Remove(descriptor);
                    }
                });
            });

        Client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        _ = await Client.GetAsync("/health/live");

        // Default client: the seeded demo company's owner (development-only demo sign-in).
        var demo = await (await Client.PostAsync("/api/v1/account/demo", null)).Content.ReadFromJsonAsync<JsonElement>();
        Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", demo.GetProperty("accessToken").GetString());
    }

    /// <summary>A client with no sign-in.</summary>
    public HttpClient AnonymousClient() => Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    /// <summary>A client signed in with this session's access token.</summary>
    public HttpClient ClientFor(string accessToken)
    {
        var client = AnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
        await _postgres.DisposeAsync();
        if (Directory.Exists(StorageRoot))
        {
            Directory.Delete(StorageRoot, recursive: true);
        }
    }
}

[CollectionDefinition(Name)]
public sealed class OmniOpsApiCollection : ICollectionFixture<OmniOpsApiFixture>
{
    public const string Name = "OmniOpsApi";
}
