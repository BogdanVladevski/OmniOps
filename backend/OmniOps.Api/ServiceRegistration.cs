using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OmniOps.Api.Configuration;
using FluentValidation;
using OmniOps.Application.Behaviours;
using OmniOps.Application.Commands;
using OmniOps.Application.Validators;
using OmniOps.Infrastructure;
using OmniOps.Infrastructure.BackgroundWorkers;
using OmniOps.Infrastructure.Configuration;
using OmniOps.Infrastructure.Data;
using OmniOps.Infrastructure.Data.Interceptors;
using OmniOps.Infrastructure.Services;
using OmniOps.Core.Interfaces;
using StackExchange.Redis;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Npgsql;
using OmniOps.Infrastructure.Observability;
using Serilog;

namespace OmniOps.Api;

public static partial class ServiceRegistration
{
    public static void ConfigureServices(WebApplicationBuilder builder)
    {
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddKafkaResilience();

        var databaseOptions = builder.Configuration
            .GetSection(DatabaseOptions.SectionName)
            .Get<DatabaseOptions>() ?? new DatabaseOptions();
        var connectionString = EnvironmentConfiguration.ResolveConnectionString(databaseOptions);

        var redisOptions = builder.Configuration
            .GetSection(RedisOptions.SectionName)
            .Get<RedisOptions>() ?? new RedisOptions();
        var redisConnectionString = redisOptions.ResolveConnectionString();

        var kafkaOptions = builder.Configuration
            .GetSection(KafkaOptions.SectionName)
            .Get<KafkaOptions>() ?? new KafkaOptions();

        builder.Services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString);
            options.AddInterceptors(sp.GetRequiredService<OutboxSaveChangesInterceptor>());
        });

        builder.Services.AddSingleton<OutboxSaveChangesInterceptor>();

        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = redisOptions.InstanceName;
        });

        builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConnectionString));

        builder.Services.AddTransient<ITelemetryCacheService, RedisTelemetryCacheService>();
        builder.Services.AddTransient<IAnomalyDetectionService, RedisAnomalyDetectionService>();
        builder.Services.AddTransient<IDeduplicationService, RedisDeduplicationService>();
        builder.Services.AddTransient<ITelemetryRepository, TelemetryRepository>();
        builder.Services.AddTransient<ITelemetryBroadcastService, SignalRTelemetryBroadcastService>();
        builder.Services.AddTransient<IPlaybookOrchestrationService, PlaybookOrchestrationService>();
        builder.Services.AddSingleton<ITelemetryMetrics, TelemetryMetrics>();
        builder.Services.AddSingleton<IFleetVehicleRegistry, FleetVehicleRegistry>();

        builder.Services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(ProcessTelemetryCommand).Assembly);
            cfg.AddOpenBehavior(typeof(LoggingBehaviour<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehaviour<,>));
        });

        builder.Services.AddValidatorsFromAssemblyContaining<ProcessTelemetryCommandValidator>();

        builder.Services.AddSingleton<KafkaTelemetryMessageProcessor>();
        builder.Services.AddHostedService<KafkaTelemetryConsumer>();
        builder.Services.AddHostedService<OutboxPublisherWorker>();
        builder.Services.AddSignalR()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.PropertyNamingPolicy =
                    System.Text.Json.JsonNamingPolicy.CamelCase;
            });
        builder.Services.AddOpenApi();

        ConfigureCors(builder);
        ConfigureAuthentication(builder);
        ConfigureRateLimiting(builder);
        ConfigureHealthChecks(builder, connectionString, redisConnectionString);
        ConfigureKafkaProducer(builder, kafkaOptions);
        ConfigureObservability(builder);
    }

    private static void ConfigureCors(WebApplicationBuilder builder)
    {
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyHeader().AllowAnyMethod();

                if (builder.Environment.IsDevelopment())
                {
                    policy.SetIsOriginAllowed(_ => true).AllowCredentials();
                }
                else
                {
                    var corsOptions = builder.Configuration
                        .GetSection(CorsOptions.SectionName)
                        .Get<CorsOptions>() ?? new CorsOptions();
                    var origins = corsOptions.GetOriginsArray();

                    if (origins.Length > 0)
                    {
                        policy.WithOrigins(origins).AllowCredentials();
                    }
                }
            });
        });
    }

    private static void ConfigureKafkaProducer(WebApplicationBuilder builder, KafkaOptions kafkaOptions)
    {
        builder.Services.AddSingleton<IProducer<Null, string>>(_ =>
        {
            var producerConfig = new ProducerConfig
            {
                BootstrapServers = kafkaOptions.BootstrapServers,
                Acks = Acks.All,
                AllowAutoCreateTopics = true
            };
            return new ProducerBuilder<Null, string>(producerConfig).Build();
        });

        EnsureKafkaTopicsExist(kafkaOptions);
    }

    private static void EnsureKafkaTopicsExist(KafkaOptions kafkaOptions)
    {
        var adminConfig = new AdminClientConfig { BootstrapServers = kafkaOptions.BootstrapServers };

        try
        {
            using var adminClient = new AdminClientBuilder(adminConfig).Build();
            adminClient.CreateTopicsAsync([
                new TopicSpecification
                {
                    Name = kafkaOptions.Topic,
                    NumPartitions = 1,
                    ReplicationFactor = 1
                },
                new TopicSpecification
                {
                    Name = kafkaOptions.DlqTopic,
                    NumPartitions = 1,
                    ReplicationFactor = 1
                },
                new TopicSpecification
                {
                    Name = kafkaOptions.EventsTopic,
                    NumPartitions = 1,
                    ReplicationFactor = 1
                }
            ]).GetAwaiter().GetResult();
        }
        catch (CreateTopicsException e) when (e.Results.Any(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
        {
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to create Kafka topics on startup: {Message}", ex.Message);
        }
    }

    public static async Task MigrateDatabaseAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = app.Logger;

        try
        {
            logger.LogInformation("Applying EF Core database migrations");
            await context.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied successfully");
        }
        catch (PostgresException ex) when (ex.SqlState == "28P01")
        {
            throw new InvalidOperationException(
                "PostgreSQL authentication failed. The Docker volume may have been initialized with " +
                "different credentials. Reset it with: docker compose -f infra/docker-compose.yml down -v " +
                "then docker compose -f infra/docker-compose.yml up -d", ex);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Migration failed, attempting EnsureCreated fallback");
            try
            {
                await context.Database.EnsureCreatedAsync();
            }
            catch (PostgresException pgEx) when (pgEx.SqlState == "28P01")
            {
                throw new InvalidOperationException(
                    "PostgreSQL authentication failed. The Docker volume may have been initialized with " +
                    "different credentials. Reset it with: docker compose -f infra/docker-compose.yml down -v " +
                    "then docker compose -f infra/docker-compose.yml up -d", pgEx);
            }
        }
    }
}
