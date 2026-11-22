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
                        ["Jwt:RequireAuthentication"] = "false",
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
