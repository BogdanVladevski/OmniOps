namespace OmniOps.Infrastructure.Configuration;

public class ObservabilityOptions
{
    public const string SectionName = "Observability";

    public bool EnablePrometheusMetrics { get; set; } = true;

    public string PrometheusEndpoint { get; set; } = "/metrics";
}
