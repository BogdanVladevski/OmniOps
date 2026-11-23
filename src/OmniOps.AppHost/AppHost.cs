// Local development in one command: PostgreSQL (in Docker), the API and the Expo app, with the Aspire dashboard
// showing logs, traces and health for all of them.
var builder = DistributedApplication.CreateBuilder(args);

// The same major version as production (deploy/compose.yaml) and the integration tests; Aspire's default is newer.
// The volume name carries the version: a data directory can't be opened by a different major version.
var postgres = builder.AddPostgres("postgres")
    .WithImageTag("17-alpine")
    .WithDataVolume("omniops-postgres-17")
    .WithLifetime(ContainerLifetime.Persistent);
var database = postgres.AddDatabase("omniops");

// Generated once and kept in the AppHost's user secrets, so a fresh clone runs without setup.
var signingKey = builder.AddParameter("jwt-signing-key", new GenerateParameterDefault { MinLength = 48, Special = false }, secret: true, persist: true);

var api = builder.AddDotnetProject("api", "../OmniOps.Api/OmniOps.Api.csproj")
    .WithReference(database)
    .WaitFor(database)
    .WithEnvironment("Jwt__SigningKey", signingKey)
    .WithHttpHealthCheck("/health");

var app = builder.AddJavaScriptApp("app", "../../app", "web")
    .WithHttpEndpoint(port: 8081, isProxied: false)
    .WithEnvironment("EXPO_PUBLIC_API_URL", api.GetEndpoint("http"))
    .WithEnvironment("BROWSER", "none")
    .WaitFor(api);

api.WithEnvironment("PublicUrls__AppUrl", app.GetEndpoint("http"))
    .WithEnvironment("Cors__Origins__0", app.GetEndpoint("http"));

builder.Build().Run();
