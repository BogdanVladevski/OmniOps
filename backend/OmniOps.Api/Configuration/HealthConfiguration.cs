using HealthChecks.NpgSql;
using HealthChecks.Redis;
using OmniOps.Infrastructure.Health;

namespace OmniOps.Api;

public static partial class ServiceRegistration
{
    private static void ConfigureHealthChecks(WebApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
            .AddNpgSql(ResolveDatabaseConnectionString, name: "postgres", tags: ["ready"])
            .AddRedis(sp => sp.GetRequiredService<StackExchange.Redis.IConnectionMultiplexer>(), name: "redis", tags: ["ready"])
            .AddCheck<KafkaHealthCheck>("kafka", tags: ["ready"]);
    }
}
