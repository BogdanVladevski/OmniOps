using OmniOps.Api;
using OmniOps.Api.Configuration;
using OmniOps.Api.Endpoints;
using OmniOps.Api.Middleware;
using OmniOps.Infrastructure.Configuration;

EnvironmentConfiguration.LoadEnvironmentFile();

var builder = WebApplication.CreateBuilder(args);
EnvironmentConfiguration.BindEnvironmentVariables(builder.Configuration);

ServiceRegistration.ConfigureServices(builder);

var app = builder.Build();

app.UseGlobalExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHttpsRedirection();
}

app.UseCors();

var jwtOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<JwtOptions>>().Value;
if (jwtOptions.RequireAuthentication)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.UseRateLimiter();

app.MapTelemetryEndpoints();
app.MapHealthEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapAuthEndpoints();
}

await ServiceRegistration.MigrateDatabaseAsync(app);

app.Run();
