using Microsoft.Extensions.Configuration;
using OmniOps.Infrastructure.Configuration;

namespace OmniOps.Api.Configuration;

public static class EnvironmentConfiguration
{
    public static void LoadEnvironmentFile()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), ".env"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", ".env"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".env")
        };

        foreach (var path in candidates.Select(Path.GetFullPath))
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                DotNetEnv.Env.Load(path);
                return;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to parse .env file at '{path}'. " +
                    "Use semicolon-separated Npgsql connection strings without single quotes. " +
                    "Wrap values containing commas in double quotes. " +
                    $"Parser error: {ex.Message}", ex);
            }
        }

        DotNetEnv.Env.Load();
    }

    public static void BindEnvironmentVariables(IConfigurationBuilder configurationBuilder)
    {
        var envVars = new Dictionary<string, string?>();

        MapEnv(envVars, "ASPNETCORE_ENVIRONMENT", "ASPNETCORE_ENVIRONMENT");
        MapEnv(envVars, "Database:ConnectionString", "DB_CONNECTION_STRING");
        MapEnv(envVars, "Database:Host", "DB_HOST");
        MapEnv(envVars, "Database:Port", "DB_PORT");
        MapEnv(envVars, "Database:Database", "POSTGRES_DB");
        MapEnv(envVars, "Database:Username", "POSTGRES_USER");
        MapEnv(envVars, "Database:Password", "POSTGRES_PASSWORD");
        MapEnv(envVars, "Redis:ConnectionString", "REDIS_CONNECTION_STRING");
        MapEnv(envVars, "Redis:Host", "REDIS_HOST");
        MapEnv(envVars, "Redis:Port", "REDIS_PORT");
        MapEnv(envVars, "Kafka:BootstrapServers", "KAFKA_BOOTSTRAP_SERVERS");
        MapEnv(envVars, "Kafka:GroupId", "KAFKA_CONSUMER_GROUP_ID");
        MapEnv(envVars, "Kafka:Topic", "KAFKA_MAIN_TOPIC");
        MapEnv(envVars, "Kafka:EventsTopic", "KAFKA_EVENTS_TOPIC");
        MapEnv(envVars, "Kafka:DlqTopic", "KAFKA_DLQ_TOPIC");
        MapEnv(envVars, "Kafka:ProcessingRetryMaxAttempts", "KAFKA_PROCESSING_RETRY_MAX_ATTEMPTS");
        MapEnv(envVars, "Kafka:ProcessingRetryDelayMilliseconds", "KAFKA_PROCESSING_RETRY_DELAY_MS");
        MapEnv(envVars, "Cors:AllowedOrigins", "ALLOWED_CORS_ORIGINS");
        MapEnv(envVars, "Jwt:Secret", "JWT_SECRET");
        MapEnv(envVars, "Jwt:Issuer", "JWT_ISSUER");
        MapEnv(envVars, "Jwt:Audience", "JWT_AUDIENCE");
        MapEnv(envVars, "Jwt:ExpirationMinutes", "JWT_EXPIRATION_MINUTES");
        MapEnv(envVars, "Jwt:RequireAuthentication", "JWT_REQUIRE_AUTHENTICATION");
        MapEnv(envVars, "SimulateRateLimit:PermitLimit", "SIMULATE_RATE_LIMIT_PERMIT_LIMIT");
        MapEnv(envVars, "SimulateRateLimit:WindowSeconds", "SIMULATE_RATE_LIMIT_WINDOW_SECONDS");
        MapEnv(envVars, "Serilog:MinimumLevel:Default", "SERILOG_MINIMUM_LEVEL");
        MapEnv(envVars, "Observability:EnablePrometheusMetrics", "PROMETHEUS_METRICS_ENABLED");
        MapEnv(envVars, "Fleet:VehicleIds", "FLEET_VEHICLE_IDS");
        MapEnv(envVars, "Cache:SlidingExpirationMinutes", "CACHE_SLIDING_EXPIRATION_MINUTES");
        MapEnv(envVars, "Cache:AbsoluteExpirationHours", "CACHE_ABSOLUTE_EXPIRATION_HOURS");
        MapEnv(envVars, "Llm:Enabled", "LLM_ENABLED");
        MapEnv(envVars, "Llm:ApiKey", "OPENAI_API_KEY");
        MapEnv(envVars, "Llm:Model", "OPENAI_MODEL");
        MapEnv(envVars, "Llm:BaseUrl", "OPENAI_BASE_URL");
        MapEnv(envVars, "Llm:TimeoutSeconds", "LLM_TIMEOUT_SECONDS");
        MapEnv(envVars, "Llm:PlaybooksDirectory", "LLM_PLAYBOOKS_DIRECTORY");

        configurationBuilder.AddInMemoryCollection(envVars);
    }

    public static string ResolveConnectionString(DatabaseOptions options)
    {
        var connectionString = options.ResolveConnectionString();

        // Docker Compose maps Postgres 5432 -> host 5433; only rewrite for host-local connections.
        var isLocalHost = connectionString.Contains("Host=127.0.0.1", StringComparison.Ordinal)
            || connectionString.Contains("Host=localhost", StringComparison.OrdinalIgnoreCase);

        if (isLocalHost && connectionString.Contains("Port=5432;", StringComparison.Ordinal))
        {
            connectionString = connectionString.Replace("Port=5432;", "Port=5433;");
        }

        return connectionString;
    }

    private static void MapEnv(Dictionary<string, string?> target, string configKey, string envKey)
    {
        var value = Environment.GetEnvironmentVariable(envKey);
        if (!string.IsNullOrWhiteSpace(value))
        {
            target[configKey] = value;
        }
    }
}
