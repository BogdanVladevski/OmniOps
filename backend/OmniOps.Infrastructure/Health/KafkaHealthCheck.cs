using Confluent.Kafka;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OmniOps.Infrastructure.Configuration;

namespace OmniOps.Infrastructure.Health;

public class KafkaHealthCheck : IHealthCheck
{
    private readonly KafkaOptions _options;

    public KafkaHealthCheck(IOptions<KafkaOptions> options)
    {
        _options = options.Value;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var admin = new AdminClientBuilder(new AdminClientConfig
            {
                BootstrapServers = _options.BootstrapServers
            }).Build();

            var metadata = admin.GetMetadata(TimeSpan.FromSeconds(5));

            if (metadata.Brokers.Count == 0)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("No Kafka brokers available."));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                $"Kafka broker reachable ({metadata.Brokers.Count} broker(s))."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Kafka broker unreachable.", ex));
        }
    }
}
