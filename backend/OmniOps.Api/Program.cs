using OmniOps.Api;
using OmniOps.Api.Configuration;
using OmniOps.Api.Endpoints;
using OmniOps.Api.Middleware;
using OmniOps.Infrastructure.Configuration;
using Scalar.AspNetCore;
using Serilog;

EnvironmentConfiguration.LoadEnvironmentFile();

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "OmniOps.Api")
        .Enrich.WithEnvironmentName()
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}"));

    EnvironmentConfiguration.BindEnvironmentVariables(builder.Configuration);

    ServiceRegistration.ConfigureServices(builder);

    var app = builder.Build();

    app.UseCorrelationId();
    app.UseSerilogRequestLogging();
    app.UseGlobalExceptionHandler();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference(options =>
        {
            options.WithTitle("OmniOps API");
            options.WithOpenApiRoutePattern("/openapi/v1.json");
        });
    }
    else
    {
        app.UseHttpsRedirection();
    }

    app.UseCors();

    var jwtOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<JwtOptions>>().Value;
    if (!app.Environment.IsDevelopment() && !jwtOptions.RequireAuthentication)
    {
        app.Logger.LogCritical(
            "Running in non-Development environment with authentication DISABLED. " +
            "Set JWT_REQUIRE_AUTHENTICATION=true before deploying to production.");
    }

    if (jwtOptions.RequireAuthentication)
    {
        app.UseAuthentication();
        app.UseAuthorization();
    }

    app.UseRateLimiter();

    app.MapTelemetryEndpoints();
    app.MapShipmentEndpoints();
    app.MapFleetEndpoints();
    app.MapIncidentEndpoints();
    app.MapAnalyticsEndpoints();
    app.MapPredictionEndpoints();
    app.MapCopilotEndpoints();
    app.MapTenantEndpoints();
    app.MapNotificationEndpoints();
    app.MapAdminEndpoints();
    app.MapMobileEndpoints();
    app.MapDemoEndpoints();
    app.MapHealthEndpoints();
    app.MapObservabilityEndpoints();

    if (app.Environment.IsDevelopment())
    {
        app.MapAuthEndpoints();
    }

    await ServiceRegistration.MigrateDatabaseAsync(app);

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "OmniOps API terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
