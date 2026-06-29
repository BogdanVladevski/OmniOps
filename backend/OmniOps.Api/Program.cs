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

DotNetEnv.Env.Load();


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi


//Database and core infra layers
var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

//register Redis distributed cache pointing to our Docker container port
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "localhost:6379";
    options.InstanceName = "OmniOps_";
});

//register custom interface abstractions 
builder.Services.AddTransient<ITelemetryCacheService, RedisTelemetryCacheService>();

//App logic and orchestration layers
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(
    typeof(OmniOps.Core.Telemetry.ProcessTelemetryCommand).Assembly,
    typeof(OmniOps.Infrastructure.Handlers.ProcessTelemetryCommandHandler).Assembly
));

//Background workers/daemons (the consumers dependent on MediatR)
builder.Services.AddHostedService<KafkaTelemetryConsumer>();

//SignalR for real-time telemetry streaming to clients
builder.Services.AddSignalR();

//API Documentation and Presentation
builder.Services.AddOpenApi();

//CORS configuration to allow requests from any origin (for development purposes)
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

var adminConfig = new AdminClientConfig { BootstrapServers = "localhost:9092" };
using (var adminClient = new AdminClientBuilder(adminConfig).Build())
{
    try
    {
        // Force-create the topic with 1 partition and a replication factor of 1 if it doesn't exist
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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

//map a GET endpoint to retrieve the latest telemetry for a given vehicle ID
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


var producerConfig = new ProducerConfig { BootstrapServers = "localhost:9092", Acks = Acks.All, AllowAutoCreateTopics = true };

var sharedProducer = new ProducerBuilder<Null, string>(producerConfig).Build();

app.UseCors();

app.MapHub<TelemetryHub>("/api/stream/telemetry");

//simulate a POST endpoint to generate mock telemetry data for a given vehicle ID and send it to Kafka
app.MapPost("/api/test/simulate/{vehicleId}", async (string vehicleId, int packets) =>
{
    var random = new Random();
    int successfullySent = 0;

    for (int i = 0; i < packets; i++)
    {
        var mockTelemetry = new VehicleTelemetry
        {
            VehicleId = vehicleId,
            Latitude = 41.9981 + (random.NextDouble() - 0.5) * 0.05,  //simulating a small variation in latitude (example for Skopje)
            Longitude = 21.4254 + (random.NextDouble() - 0.5) * 0.05,
            Speed = random.Next(50, 120),
            FuelLevel = Math.Round(80.0 - (i * 0.5), 2),
            EngineTemperature = random.Next(85, 105),
            Timestamp = DateTime.UtcNow
        };

        var jsonPayload = JsonSerializer.Serialize(mockTelemetry);

        try
        {
            //block synchronously to guarantee the message cuts through to Docker before continuing
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

        //force network flush to ensure the message is sent to Kafka before continuing
        sharedProducer.Flush(TimeSpan.FromMilliseconds(100));
    }

    return Results.Ok(new { Message = $"Successfully queued {successfullySent} mock telemetry packets into Kafka stream." });
})
.WithName("SimulateVehicleTelemetry")
.WithOpenApi();



using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        Console.WriteLine("[STARTUP] Synchronizing PostgreSQL database schema...");
        await context.Database.MigrateAsync();
        Console.WriteLine(" [STARTUP] PostgreSQL database migrated successfully.");
    }
    catch (Exception)
    {
        Console.WriteLine("[STARTUP] No formal migrations found or history table missing. Applying direct schema creation fallback...");
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

