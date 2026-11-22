using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OmniOps.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace OmniOps.IntegrationTests.Infrastructure;

/// <summary>
/// The real API against a real PostgreSQL in Docker, signed in exactly as in production. Background jobs are off;
/// tests that need them send the command themselves.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    /// <summary>Uploaded files go to a throwaway folder, never into the source tree.</summary>
    public string StorageRoot { get; } = Path.Combine(Path.GetTempPath(), "omniops-it-" + Guid.NewGuid().ToString("N"));

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:omniops"] = _postgres.GetConnectionString(),
                ["Jwt:SigningKey"] = "integration-tests-signing-key-0123456789abcdef",
                ["RateLimits:AccountsPerMinute"] = "100000",
                ["Storage:RootPath"] = StorageRoot,
            }));
            builder.ConfigureServices(services =>
            {
                foreach (var hosted in services.Where(d => d.ServiceType == typeof(IHostedService)).ToList())
                {
                    services.Remove(hosted);
                }
            });
        });

        // Starts the app (and applies migrations).
        (await Anonymous().GetAsync("/alive")).EnsureSuccessStatusCode();
    }

    public HttpClient Anonymous() => Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public HttpClient SignedIn(string accessToken)
    {
        var client = Anonymous();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    /// <summary>A fresh company with its owner signed in.</summary>
    public async Task<Company> NewCompanyAsync(string name = "Test Haulage")
    {
        var email = $"{Guid.NewGuid():N}@example.test";
        var session = await (await Anonymous().PostAsJsonAsync("/api/v1/account/sign-up",
            new { companyName = name, name = "Owner", email, password = Company.Password })).ReadJsonAsync();
        return new Company(this, SignedIn(session.GetProperty("accessToken").GetString()!), session, email);
    }

    /// <summary>Direct database access, for setting up what the API can't (e.g. a job created hours ago).</summary>
    public async Task WithDatabaseAsync(Func<AppDbContext, Task> work)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>Sends a system command (what a background job would do), outside any sign-in.</summary>
    public async Task<T> SendAsync<T>(Mediator.ICommand<T> command)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Mediator.ISender>().Send(command);
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _postgres.DisposeAsync();
        if (Directory.Exists(StorageRoot))
        {
            Directory.Delete(StorageRoot, recursive: true);
        }
    }

    public Task BackdateJobAsync(Guid jobId, TimeSpan by) => WithDatabaseAsync(db =>
        db.Jobs.IgnoreQueryFilters().Where(j => j.Id == jobId).ExecuteUpdateAsync(j => j.SetProperty(x => x.CreatedAtUtc, DateTime.UtcNow - by)));
}

[CollectionDefinition(Name)]
public sealed class ApiTestGroup : ICollectionFixture<ApiFixture>
{
    public const string Name = "Api";
}
