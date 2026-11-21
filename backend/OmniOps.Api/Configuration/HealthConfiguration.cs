using HealthChecks.NpgSql;

namespace OmniOps.Api;

public static partial class ServiceRegistration
{
    private static void ConfigureHealthChecks(WebApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
            .AddNpgSql(ResolveDatabaseConnectionString, name: "postgres", tags: ["ready"]);
    }
}
