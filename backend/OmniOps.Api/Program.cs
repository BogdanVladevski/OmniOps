using OmniOps.Api;
using OmniOps.Api.Configuration;
using OmniOps.Api.Endpoints;
using OmniOps.Api.Middleware;
using OmniOps.Infrastructure.Configuration;
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

    app.UseSerilogRequestLogging();
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
