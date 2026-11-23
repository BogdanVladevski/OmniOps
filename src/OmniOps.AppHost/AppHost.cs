// Local development in one command: PostgreSQL (in Docker), the API and the Expo app, with the Aspire dashboard
// showing logs, traces and health for all of them.
//
// Phone mode (OMNIOPS_PHONE=1): the API and the app's dev server are reachable from your Wi-Fi, so the app runs on a
// real phone in Expo Go with GPS and camera. Links in emails and messages then use this computer's network address.
var builder = DistributedApplication.CreateBuilder(args);

var phone = builder.Configuration["OMNIOPS_PHONE"] is "1" or "true";
var lan = phone
    ? builder.Configuration["OMNIOPS_LAN_HOST"] ?? LocalNetwork.Address()
      ?? throw new InvalidOperationException("Phone mode: no Wi-Fi or Ethernet address found. Set OMNIOPS_LAN_HOST to this computer's IP.")
    : null;

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

// Phones run the native app ("start"); the web build is still served on the same port.
var app = builder.AddJavaScriptApp("app", "../../app", phone ? "start" : "web")
    .WithHttpEndpoint(port: 8081, isProxied: false)
    .WithEnvironment("BROWSER", "none")
    .WaitFor(api);

if (lan is null)
{
    app.WithEnvironment("EXPO_PUBLIC_API_URL", api.GetEndpoint("http"));
    api.WithEnvironment("PublicUrls__AppUrl", app.GetEndpoint("http"))
        .WithEnvironment("Cors__Origins__0", app.GetEndpoint("http"));
}
else
{
    var apiUrl = $"http://{lan}:5031";
    var appUrl = $"http://{lan}:8081";

    // Listen on every interface (not only this computer) without the dashboard's proxy in between.
    api.WithEndpoint("http", e =>
        {
            e.IsProxied = false;
            e.TargetHost = "0.0.0.0";
        })
        .WithEnvironment("PublicUrls__ApiUrl", apiUrl)
        .WithEnvironment("PublicUrls__AppUrl", appUrl)
        .WithEnvironment("Cors__Origins__0", "http://localhost:8081")
        .WithEnvironment("Cors__Origins__1", appUrl);

    app.WithEnvironment("EXPO_PUBLIC_API_URL", apiUrl)
        .WithEnvironment("REACT_NATIVE_PACKAGER_HOSTNAME", lan) // the address Expo Go is told to load from
        .WithUrl($"exp://{lan}:8081", "Open in Expo Go");
}

builder.Build().Run();
