using Microsoft.EntityFrameworkCore;
using OmniOps.Api.Authentication;
using OmniOps.Api.Endpoints;
using OmniOps.Api.Infrastructure;
using OmniOps.Application;
using OmniOps.Application.Common.Behaviours;
using OmniOps.Infrastructure;
using OmniOps.Infrastructure.Persistence;
using OmniOps.Infrastructure.Realtime;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddInfrastructure();
builder.Services.AddApplication();
builder.Services.AddMediator(options =>
{
    options.ServiceLifetime = ServiceLifetime.Scoped;
    options.PipelineBehaviors =
    [
        typeof(LoggingBehaviour<,>),
        typeof(AuthorizationBehaviour<,>),
        typeof(ValidationBehaviour<,>),
        typeof(TransactionBehaviour<,>),
    ];
});

// Numbers are numbers: no "12.5" strings accepted, so the OpenAPI contract (and the app's types) say number.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.Strict);

builder.Services.AddOmniAuthentication();
builder.Services.AddOmniRateLimits();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseStaticFiles();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference("/docs", options => options.WithTitle("OmniOps API"));
}

app.MapDefaultEndpoints();
app.MapHub<LiveHub>(LiveHub.Path);
app.MapAccountEndpoints();
app.MapFleetEndpoints();
app.MapJobEndpoints();
app.MapDriverEndpoints();
app.MapBillingEndpoints();
app.MapAuditEndpoints();
app.MapDemoEndpoints();
app.MapPublicEndpoints();

if (!BuildContext.IsGeneratingOpenApiDocument && app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

await app.RunAsync();

/// <summary>Entry point; public for the integration tests' WebApplicationFactory.</summary>
public partial class Program;
