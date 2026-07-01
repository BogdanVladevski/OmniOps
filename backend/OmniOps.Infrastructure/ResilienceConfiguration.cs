using Microsoft.Extensions.DependencyInjection;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Health;
using OmniOps.Infrastructure.Resilience;
using OmniOps.Infrastructure.Services;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace OmniOps.Infrastructure;

public static class KafkaResilienceServiceCollectionExtensions
{
    public static IServiceCollection AddKafkaResilience(this IServiceCollection services)
    {
        services.AddResiliencePipeline(KafkaResiliencePipeline.Name, (builder, _) =>
        {
            builder
                .AddRetry(new RetryStrategyOptions
                {
                    ShouldHandle = new PredicateBuilder().Handle<Exception>(
                        ex => ex is not OperationCanceledException),
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromMilliseconds(200),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true
                })
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    ShouldHandle = new PredicateBuilder().Handle<Exception>(
                        ex => ex is not OperationCanceledException),
                    FailureRatio = 0.5,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    MinimumThroughput = 5,
                    BreakDuration = TimeSpan.FromSeconds(30)
                });
        });

        services.AddSingleton<IKafkaMessageProducer, ResilientKafkaMessageProducer>();
        services.AddSingleton<KafkaHealthCheck>();

        return services;
    }
}
