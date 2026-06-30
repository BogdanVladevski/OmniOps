using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;
using OmniOps.Core.Telemetry;
using OmniOps.Infrastructure.BackgroundWorkers;
using OmniOps.Infrastructure.Data;
using OmniOps.Infrastructure.Services;
using OmniOps.Infrastructure.Hubs;
using DotNetEnv;

// -------------------------------------------------------------------------
// 1. SECURE ENVIRONMENT INITIALIZATION
// -------------------------------------------------------------------------
var envPath = "/home/bogdan/Documents/OmniOps/.env";

if (File.Exists(envPath))
{
    DotNetEnv.Env.Load(envPath);
    Console.WriteLine($"[STARTUP] Secure environment loaded cleanly from: {envPath}");
}
else
{
    DotNetEnv.Env.Load();
    Console.WriteLine("[STARTUP WARNING] Target root .env not found. Using local directory fallback.");
}

var builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// 2. DATABASE CONFIGURATION (FORCED DOCKER HOST PORT 5433 OVERRIDE)
// -------------------------------------------------------------------------
// We explicitly fall back to port 5433 matching your 'docker ps' runtime mapping
var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
                       ?? "Host=127.0.0.1;Port=5433;Database=OmniOps;Username=postgres;Password=123";

if (connectionString.Contains("Port=5432;"))
{
    connectionString = connectionString.Replace("Port=5432;", "Port=5433;");
}

Console.WriteLine($"[STARTUP DIAGNOSTIC] Core EF Context routing targeted at: {connectionString}");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// -------------------------------------------------------------------------
// 3. CACHING & LAYER SUBSCRIPTIONS
// -------------------------------------------------------------------------
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "127.0.0.1:6379";
    options.InstanceName = "OmniOps_";
});

builder.Services.AddTransient<ITelemetryCacheService, RedisTelemetryCacheService>();

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(
    typeof(OmniOps.Core.Telemetry.ProcessTelemetryCommand).Assembly,
    typeof(OmniOps.Infrastructure.Handlers.ProcessTelemetryCommandHandler).Assembly
));

builder.Services.AddHostedService<KafkaTelemetryConsumer>();
builder.Services.AddSignalR();
builder.Services.AddOpenApi();

// -------------------------------------------------------------------------
// 4. SECURITY & CROSS-ORIGIN RESOURCE SHARING (CORS)
// -------------------------------------------------------------------------
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .SetIsOriginAllowed(origin => true)
              .AllowCredentials();
    });
});

// -------------------------------------------------------------------------
// 5. KAFKA CLUSTER TOPOLOGY INITIALIZATION
// -------------------------------------------------------------------------
var adminConfig = new AdminClientConfig { BootstrapServers = "127.0.0.1:9092" };
using (var adminClient = new AdminClientBuilder(adminConfig).Build())
{
    try
    {
        adminClient.CreateTopicsAsync(new TopicSpecification[] {
            new TopicSpecification { Name = "fleet-telemetry", NumPartitions = 1, ReplicationFactor = 1 }
        }).GetAwaiter().GetResult();

        Console.WriteLine("[STARTUP] 'fleet-telemetry' topic verified/created successfully.");
    }
    catch (CreateTopicsException e) when (e.Results[0].Error.Code == ErrorCode.TopicAlreadyExists)
    {
        Console.WriteLine("[STARTUP] 'fleet-telemetry' topic already exists. Proceeding...");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[STARTUP WARNING] Could not auto-initialize topic: {ex.Message}");
    }
}

var app = builder.Build();

// -------------------------------------------------------------------------
// 6. HTTP REQUEST PIPELINE MIDDLEWARE
// -------------------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors();

// -------------------------------------------------------------------------
// 7. MINIMAL API ENDPOINT ROUTING
// -------------------------------------------------------------------------

// GET: Fetch cached tracking profile
app.MapGet("/api/telemetry/{vehicleId}", async (string vehicleId, IMediator mediator) =>
{
    var query = new GetLatestTelemetryQuery(vehicleId);
    var result = await mediator.Send(query);

    return result is not null
        ? Results.Ok(result)
        : Results.NotFound(new { Message = $"No telemetry profile cached for vehicle: {vehicleId}" });
})
.WithName("GetLatestVehicleTelemetry")
.WithOpenApi();

// Real-time Event Streaming Hub
app.MapHub<TelemetryHub>("/api/stream/telemetry");

// POST: Run network simulator and send stream traffic straight into Kafka Broker
var producerConfig = new ProducerConfig { BootstrapServers = "127.0.0.1:9092", Acks = Acks.All, AllowAutoCreateTopics = true };
var sharedProducer = new ProducerBuilder<Null, string>(producerConfig).Build();

app.MapPost("/api/test/simulate/{vehicleId}", async (string vehicleId, int packets) =>
{
    var random = new Random();
    int successfullySent = 0;

    for (int i = 0; i < packets; i++)
    {
        var mockTelemetry = new VehicleTelemetry
        {
            VehicleId = vehicleId,
            Latitude = 41.9981 + (random.NextDouble() - 0.5) * 0.05,
            Longitude = 21.4254 + (random.NextDouble() - 0.5) * 0.05,
            Speed = random.Next(50, 120),
            FuelLevel = Math.Round(80.0 - (i * 0.5), 2),
            EngineTemperature = random.Next(85, 105),
            Timestamp = DateTime.UtcNow
        };

        var jsonPayload = JsonSerializer.Serialize(mockTelemetry);

        try
        {
            var deliveryResult = sharedProducer.ProduceAsync("fleet-telemetry",
                new Message<Null, string> { Value = jsonPayload }).GetAwaiter().GetResult();

            if (deliveryResult.Status == PersistenceStatus.Persisted)
            {
                successfullySent++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Simulator Kafka Error]: {ex.Message}");
        }

        sharedProducer.Flush(TimeSpan.FromMilliseconds(100));
    }

    return Results.Ok(new { Message = $"Successfully queued {successfullySent} mock telemetry packets into Kafka stream." });
})
.WithName("SimulateVehicleTelemetry")
.WithOpenApi();

// -------------------------------------------------------------------------
// 8. DATA CONTEXT AUTOMIGRATION ROUTINE
// -------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        Console.WriteLine("[STARTUP] Synchronizing PostgreSQL database schema via EF Core...");
        await context.Database.MigrateAsync();
        Console.WriteLine("[STARTUP] PostgreSQL database migrated successfully.");
    }
    catch (Exception)
    {
        Console.WriteLine("[STARTUP] No formal migrations found. Applying direct schema creation fallback...");
        try
        {
            var context = services.GetRequiredService<AppDbContext>();
            await context.Database.EnsureCreatedAsync();
            Console.WriteLine("[STARTUP] Database tables generated directly from DbContext models successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[STARTUP CRITICAL ERROR] Direct creation fallback failed: {ex.Message}");
        }
    }
}

app.Run();