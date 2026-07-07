using Microsoft.Extensions.Options;
using OmniOps.Infrastructure.Configuration;
using OmniOps.Infrastructure.Observability;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OmniOps.Api;

public static partial class ServiceRegistration
{
    private static void ConfigureObservability(WebApplicationBuilder builder)
    {
        builder.Services.Configure<ObservabilityOptions>(
            builder.Configuration.GetSection(ObservabilityOptions.SectionName));

        var observability = builder.Configuration
            .GetSection(ObservabilityOptions.SectionName)
            .Get<ObservabilityOptions>() ?? new ObservabilityOptions();

        var openTelemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("OmniOps.Api"));

        openTelemetry.WithTracing(tracing =>
        {
            tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddSource("OmniOps.Api.Simulator")
                .AddSource("OmniOps.Infrastructure.TelemetryConsumer")
                .AddSource("OmniOps.Infrastructure.Kafka");

            if (builder.Environment.IsDevelopment())
            {
                tracing.AddConsoleExporter();
            }
        });

        if (observability.EnablePrometheusMetrics)
        {
            openTelemetry.WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(TelemetryMetrics.MeterName)
                .AddPrometheusExporter());
        }
    }

    public static void MapObservabilityEndpoints(this WebApplication app)
    {
        var observability = app.Services
            .GetRequiredService<IOptions<ObservabilityOptions>>().Value;

        if (observability.EnablePrometheusMetrics)
        {
            app.MapPrometheusScrapingEndpoint(observability.PrometheusEndpoint);
        }
    }
}
