using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OmniOps.Core.Entities;
using OmniOps.Core.DTOs;
using OmniOps.Core.Interfaces;
using OmniOps.Core.Telemetry;
using OmniOps.Infrastructure.BackgroundWorkers;
using OmniOps.Infrastructure.Configuration;
using OmniOps.Infrastructure.Data;
using OmniOps.Infrastructure.Services;
using OmniOps.Infrastructure.Hubs;
using StackExchange.Redis;
using DotNetEnv;

// -------------------------------------------------------------------------
// 1. SECURE ENVIRONMENT INITIALIZATION & STARTUP LOGGER
// -------------------------------------------------------------------------
using var startupLoggerFactory = LoggerFactory.Create(loggingBuilder => 
{
    loggingBuilder.AddConsole();
});
var startupLogger = startupLoggerFactory.CreateLogger("Program");

var envPath = "/home/bogdan/Documents/OmniOps/.env";

if (File.Exists(envPath))
{
    DotNetEnv.Env.Load(envPath);
    startupLogger.LogInformation("Secure environment loaded cleanly from: {EnvPath}", envPath);
}
else
{
    DotNetEnv.Env.Load();
    startupLogger.LogWarning("Target root .env not found. Using local directory fallback.");
}

var builder = WebApplication.CreateBuilder(args);

// Register Infrastructure configuration options
builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection(KafkaOptions.SectionName));
builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection(RedisOptions.SectionName));

// Resolve Redis options for configuration
var redisOptions = new RedisOptions();
builder.Configuration.GetSection(RedisOptions.SectionName).Bind(redisOptions);
redisOptions.Configuration = Environment.GetEnvironmentVariable("REDIS_CONFIGURATION") ?? redisOptions.Configuration;
redisOptions.InstanceName = Environment.GetEnvironmentVariable("REDIS_INSTANCE_NAME") ?? redisOptions.InstanceName;

var kafkaOptions = new KafkaOptions();
builder.Configuration.GetSection(KafkaOptions.SectionName).Bind(kafkaOptions);
kafkaOptions.BootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS") ?? kafkaOptions.BootstrapServers;
kafkaOptions.Topic = Environment.GetEnvironmentVariable("KAFKA_TOPIC") ?? kafkaOptions.Topic;
kafkaOptions.GroupId = Environment.GetEnvironmentVariable("KAFKA_GROUP_ID") ?? kafkaOptions.GroupId;

// -------------------------------------------------------------------------
// 2. DATABASE CONFIGURATION (FORCED DOCKER HOST PORT 5433 OVERRIDE)
// -------------------------------------------------------------------------
var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
                       ?? "Host=127.0.0.1;Port=5433;Database=OmniOps;Username=postgres;Password=123";

if (connectionString.Contains("Port=5432;"))
{
    connectionString = connectionString.Replace("Port=5432;", "Port=5433;");
}

startupLogger.LogInformation("Core EF Context routing targeted at: {ConnectionString}", connectionString);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// -------------------------------------------------------------------------
// 3. CACHING & LAYER SUBSCRIPTIONS
// -------------------------------------------------------------------------
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisOptions.Configuration;
    options.InstanceName = redisOptions.InstanceName;
});

// Register Singleton IConnectionMultiplexer for advanced Redis commands (dedup, sliding window)
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    startupLogger.LogInformation("Connecting to Redis Multiplexer at: {Config}", redisOptions.Configuration);
    return ConnectionMultiplexer.Connect(redisOptions.Configuration);
});

builder.Services.AddTransient<ITelemetryCacheService, RedisTelemetryCacheService>();
builder.Services.AddTransient<IAnomalyDetectionService, RedisAnomalyDetectionService>();

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(
    typeof(OmniOps.Core.Telemetry.ProcessTelemetryCommand).Assembly,
    typeof(OmniOps.Infrastructure.Handlers.ProcessTelemetryCommandHandler).Assembly
));

builder.Services.AddHostedService<KafkaTelemetryConsumer>();
builder.Services.AddHostedService<OutboxPublisherWorker>();
builder.Services.AddSignalR();
builder.Services.AddOpenApi();

// ActivitySource for simulator distributed tracing
ActivitySource simulatorSource = new("OmniOps.Api.Simulator");

// -------------------------------------------------------------------------
// 4. SECURITY & CROSS-ORIGIN RESOURCE SHARING (CORS)
// -------------------------------------------------------------------------
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod();

        if (builder.Environment.IsDevelopment())
        {
            policy.SetIsOriginAllowed(origin => true)
                  .AllowCredentials();
        }
        else
        {
            var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
                                 ?? Array.Empty<string>();
            if (allowedOrigins.Length > 0)
            {
                policy.WithOrigins(allowedOrigins)
                      .AllowCredentials();
            }
            else
            {
                // Strict fallback: no allowed origins if configuration is missing in production
                policy.WithOrigins();
            }
        }
    });
});

// Register Kafka Producer as a Singleton in DI for automatic lifecycle/disposal management
builder.Services.AddSingleton<IProducer<Null, string>>(sp =>
{
    var producerConfig = new ProducerConfig 
    { 
        BootstrapServers = kafkaOptions.BootstrapServers, 
        Acks = Acks.All, 
        AllowAutoCreateTopics = true 
    };
    return new ProducerBuilder<Null, string>(producerConfig).Build();
});

// -------------------------------------------------------------------------
// 5. KAFKA CLUSTER TOPOLOGY INITIALIZATION
// -------------------------------------------------------------------------
var adminConfig = new AdminClientConfig { BootstrapServers = kafkaOptions.BootstrapServers };
using (var adminClient = new AdminClientBuilder(adminConfig).Build())
{
    try
    {
        adminClient.CreateTopicsAsync(new TopicSpecification[] {
            new TopicSpecification { Name = kafkaOptions.Topic, NumPartitions = 1, ReplicationFactor = 1 },
            new TopicSpecification { Name = kafkaOptions.Topic + "-dlq", NumPartitions = 1, ReplicationFactor = 1 }
        }).GetAwaiter().GetResult();

        startupLogger.LogInformation("Topics '{Topic}' and '{Topic}-dlq' verified/created successfully.", kafkaOptions.Topic, kafkaOptions.Topic);
    }
    catch (CreateTopicsException e) when (e.Results[0].Error.Code == ErrorCode.TopicAlreadyExists)
    {
        startupLogger.LogInformation("Topics already exist. Proceeding...");
    }
    catch (Exception ex)
    {
        startupLogger.LogWarning("Could not auto-initialize topics: {Message}", ex.Message);
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
    // Input validation for vehicleId
    if (string.IsNullOrWhiteSpace(vehicleId) || !Regex.IsMatch(vehicleId, "^[a-zA-Z0-9]+$"))
    {
        return Results.BadRequest(new { Message = "Vehicle ID must be a non-empty alphanumeric string." });
    }

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
app.MapPost("/api/test/simulate/{vehicleId}", async (
    string vehicleId, 
    int packets, 
    IProducer<Null, string> producer, 
    ILogger<Program> logger,
    IOptions<KafkaOptions> kOptions) =>
{
    // 1. Input Validation
    if (string.IsNullOrWhiteSpace(vehicleId) || !Regex.IsMatch(vehicleId, "^[a-zA-Z0-9]+$"))
    {
        return Results.BadRequest(new { Message = "Vehicle ID must be a non-empty alphanumeric string." });
    }

    if (packets <= 0 || packets > 100)
    {
        return Results.BadRequest(new { Message = "Packets count must be between 1 and 100." });
    }

    using var activity = simulatorSource.StartActivity("SimulateTelemetryStream");
    activity?.SetTag("vehicle.id", vehicleId);
    activity?.SetTag("packets.count", packets);

    var targetTopic = kOptions.Value.Topic;
    var random = new Random();
    int successfullySent = 0;

    for (int i = 0; i < packets; i++)
    {
        var mockTelemetry = new VehicleTelemetry
        {
            Id = Guid.NewGuid(), // Generate unique packet ID at producer side
            VehicleId = vehicleId,
            Latitude = 41.9981 + (random.NextDouble() - 0.5) * 0.05,
            Longitude = 21.4254 + (random.NextDouble() - 0.5) * 0.05,
            Speed = random.Next(50, 120),
            FuelLevel = Math.Round(80.0 - (i * 0.5), 2),
            EngineTemperature = random.Next(85, 105),
            Timestamp = DateTime.UtcNow
        };

        var jsonPayload = JsonSerializer.Serialize(mockTelemetry);

        var message = new Message<Null, string> { Value = jsonPayload };

        // Inject traceparent if trace activity is active
        var currentActivity = Activity.Current;
        if (currentActivity != null)
        {
            message.Headers ??= new Headers();
            message.Headers.Add("traceparent", System.Text.Encoding.UTF8.GetBytes(currentActivity.Id ?? string.Empty));
        }

        try
        {
            var deliveryResult = await producer.ProduceAsync(targetTopic, message);

            if (deliveryResult.Status == PersistenceStatus.Persisted)
            {
                successfullySent++;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Simulator Kafka Error: {Message}", ex.Message);
        }

        producer.Flush(TimeSpan.FromMilliseconds(100));
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
        app.Logger.LogInformation("Synchronizing PostgreSQL database schema via EF Core...");
        await context.Database.MigrateAsync();
        app.Logger.LogInformation("PostgreSQL database migrated successfully.");
    }
    catch (Exception)
    {
        app.Logger.LogWarning("No formal migrations found. Applying direct schema creation fallback...");
        try
        {
            var context = services.GetRequiredService<AppDbContext>();
            await context.Database.EnsureCreatedAsync();
            app.Logger.LogInformation("Database tables generated directly from DbContext models successfully!");
        }
        catch (Exception ex)
        {
            app.Logger.LogCritical(ex, "Direct creation fallback failed: {Message}", ex.Message);
        }
    }
}

app.Run();