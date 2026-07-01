using HealthChecks.NpgSql;
using HealthChecks.Redis;
using OmniOps.Infrastructure.Health;

namespace OmniOps.Api;

public static partial class ServiceRegistration
{
    private static void ConfigureHealthChecks(
        WebApplicationBuilder builder,
        string connectionString,
        string redisConnectionString)
    {
        builder.Services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgres", tags: ["ready"])
            .AddRedis(redisConnectionString, name: "redis", tags: ["ready"])
            .AddCheck<KafkaHealthCheck>("kafka", tags: ["ready"]);
    }
}
