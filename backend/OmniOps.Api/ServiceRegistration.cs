using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OmniOps.Api.Configuration;
using FluentValidation;
using OmniOps.Api.Authentication;
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
using Npgsql;
using OmniOps.Infrastructure.Observability;

namespace OmniOps.Api;

public static partial class ServiceRegistration
{
    public static void ConfigureServices(WebApplicationBuilder builder)
    {
        builder.Services.AddInfrastructure(builder.Configuration);

        // Connection settings are resolved from IOptions at service-creation time, not read from
        // builder.Configuration here: configuration sources added later (e.g. WebApplicationFactory
        // overrides, which apply at Build()) must win.
        builder.Services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.UseNpgsql(ResolveDatabaseConnectionString(sp));
            options.AddInterceptors(sp.GetRequiredService<OutboxSaveChangesInterceptor>());
        });

        builder.Services.AddSingleton<OutboxSaveChangesInterceptor>();
        builder.Services.AddSingleton<ICorrelationContext, CorrelationContext>();

        // Single-instance deployment: caches and per-vehicle detector state live in process memory.
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddSingleton(TimeProvider.System);

        builder.Services.AddTransient<ITelemetryCacheService, TelemetryCacheService>();
        builder.Services.AddSingleton<IAnomalyDetectionService, InMemoryAnomalyDetectionService>();
        builder.Services.AddSingleton<IDeduplicationService>(sp => new InMemoryDeduplicationService(sp.GetRequiredService<TimeProvider>()));
        builder.Services.AddSingleton<ChannelTelemetryIngestionQueue>();
        builder.Services.AddSingleton<ITelemetryIngestionQueue>(sp => sp.GetRequiredService<ChannelTelemetryIngestionQueue>());
        builder.Services.AddSingleton<IIntegrationEventPublisher, LoggingIntegrationEventPublisher>();
        builder.Services.AddTransient<ITelemetryRepository, TelemetryRepository>();
        builder.Services.AddTransient<IShipmentRepository, ShipmentRepository>();
        builder.Services.AddTransient<IFleetRepository, FleetRepository>();
        builder.Services.AddTransient<IVehicleRepository, VehicleRepository>();
        builder.Services.AddTransient<IDriverRepository, DriverRepository>();
        builder.Services.AddTransient<ITripRepository, TripRepository>();
        builder.Services.AddTransient<IJobRepository, JobRepository>();
        builder.Services.AddSingleton<IFileStore>(sp =>
        {
            var root = sp.GetRequiredService<IOptions<StorageOptions>>().Value.RootPath;
            var env = sp.GetRequiredService<IWebHostEnvironment>();
            return new LocalFileStore(Path.IsPathRooted(root) ? root : Path.Combine(env.ContentRootPath, root));
        });
        builder.Services.AddTransient<IDepotRepository, DepotRepository>();
        builder.Services.AddTransient<IGeofenceRepository, GeofenceRepository>();
        builder.Services.AddTransient<IEventStore, EventStore>();
        builder.Services.AddTransient<IEventReplayService, EventReplayService>();
        builder.Services.AddTransient<ICacheInvalidationService, CacheInvalidationService>();
        builder.Services.AddTransient<ITransactionManager, TransactionManager>();
        builder.Services.AddTransient<IIncidentRepository, IncidentRepository>();
        builder.Services.AddTransient<IIncidentDetectionService, IncidentDetectionService>();
        builder.Services.AddTransient<IFleetAnalyticsService, FleetAnalyticsService>();
        builder.Services.AddTransient<IPredictionService, HeuristicPredictionService>();
        builder.Services.AddTransient<IAuditService, AuditService>();
        builder.Services.AddTransient<IGeofenceDetectionService, GeofenceDetectionService>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddTransient<IAuthorizationEnforcer, AuthorizationEnforcer>();
        builder.Services.AddTransient<IRefreshTokenService, RefreshTokenService>();
        builder.Services.AddScoped<ITenantContext, TenantContext>();
        builder.Services.AddTransient<ITenantRepository, TenantRepository>();
        builder.Services.AddTransient<INotificationRepository, NotificationRepository>();
        builder.Services.AddTransient<INotificationDispatcher, NotificationDispatcher>();
        builder.Services.AddTransient<IEmailSender, LogEmailSender>();
        builder.Services.AddTransient<IPushSender, LogPushSender>();
        builder.Services.AddTransient<ISmsSender, NoOpSmsSender>();
        builder.Services.AddTransient<IDeviceRegistrationRepository, DeviceRegistrationRepository>();
        builder.Services.AddTransient<IApiKeyRepository, ApiKeyRepository>();
        builder.Services.AddTransient<IAuditLogRepository, AuditLogRepository>();
        builder.Services.AddTransient<ISyncSnapshotService, SyncSnapshotService>();
        builder.Services.AddTransient<ILoginHistoryService, LoginHistoryService>();
        builder.Services.AddTransient<ITelemetryBroadcastService, SignalRTelemetryBroadcastService>();
        builder.Services.AddTransient<ITelemetrySimulatorService, TelemetrySimulatorService>();
        builder.Services.AddSingleton<ITelemetryMetrics, TelemetryMetrics>();
        builder.Services.AddSingleton<IFleetVehicleRegistry, FleetVehicleRegistry>();

        builder.Services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(ProcessTelemetryCommand).Assembly);
            cfg.AddOpenBehavior(typeof(LoggingBehaviour<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehaviour<,>));
            cfg.AddOpenBehavior(typeof(AuthorizationBehaviour<,>));
            cfg.AddOpenBehavior(typeof(TransactionBehaviour<,>));
        });

        builder.Services.AddValidatorsFromAssemblyContaining<ProcessTelemetryCommandValidator>();

        builder.Services.AddSingleton<TelemetryMessageProcessor>();
        builder.Services.AddHostedService<TelemetryIngestionWorker>();
        builder.Services.AddHostedService<OutboxPublisherWorker>();
        builder.Services.AddHostedService<NotificationDeliveryWorker>();
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
        ConfigureHealthChecks(builder);
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

    internal static string ResolveDatabaseConnectionString(IServiceProvider sp) =>
        EnvironmentConfiguration.ResolveConnectionString(sp.GetRequiredService<IOptions<DatabaseOptions>>().Value);

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
