using System.Text.Json;
using Confluent.Kafka;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OmniOps.Core.Entities;
using OmniOps.Core.Telemetry;

namespace OmniOps.Infrastructure.BackgroundWorkers;

public class KafkaTelemetryConsumer : BackgroundService
{
    private readonly ILogger<KafkaTelemetryConsumer> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConsumerConfig _config;
    private IConsumer<Ignore, string>? _kafkaConsumer;
    private const string Topic = "fleet-telemetry";

    public KafkaTelemetryConsumer(ILogger<KafkaTelemetryConsumer> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;

        _config = new ConsumerConfig
        {
            BootstrapServers = "localhost:9092",
            GroupId = "omniops-consumer-group-v2",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("=== [DIAGNOSTIC] KafkaTelemetryConsumer.ExecuteAsync has started! ===");

        Task.Run(async () =>
        {
            try
            {
                Console.WriteLine($"=== [DIAGNOSTIC] Attempting to build Kafka Consumer on {_config.BootstrapServers} ===");
                _kafkaConsumer = new ConsumerBuilder<Ignore, string>(_config).Build();

                Console.WriteLine($"=== [DIAGNOSTIC] Subscribing to topic: {Topic} ===");
                _kafkaConsumer.Subscribe(Topic);
                _logger.LogInformation("🚀 Kafka Telemetry Consumer successfully subscribed to topic: {Topic}", Topic);

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        Console.WriteLine("=== [DIAGNOSTIC] Waiting/Polling for message from Kafka... ===");

                        // Blocking call
                        var consumeResult = _kafkaConsumer.Consume(stoppingToken);
                        if (consumeResult == null) continue;

                        Console.WriteLine($"=== [DIAGNOSTIC] RAW MESSAGE ARRIVED: {consumeResult.Message.Value} ===");
                        _logger.LogInformation("📥 Message received from Kafka: {Message}", consumeResult.Message.Value);

                        var telemetry = JsonSerializer.Deserialize<VehicleTelemetry>(consumeResult.Message.Value, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (telemetry != null)
                        {
                            Console.WriteLine($"=== [DIAGNOSTIC] Handing off {telemetry.VehicleId} to MediatR ===");
                            using var scope = _serviceProvider.CreateScope();
                            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                            await mediator.Send(new ProcessTelemetryCommand(telemetry), stoppingToken);
                            Console.WriteLine("=== [DIAGNOSTIC] MediatR Command Completed successfully ===");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Console.WriteLine("=== [DIAGNOSTIC] Consumer loop canceled gracefully ===");
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"=== [DIAGNOSTIC INNER ERROR]: {ex.Message} ===");
                        _logger.LogError(ex, "❌ Error encountered while processing Kafka telemetry stream packet.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"=== [DIAGNOSTIC CRITICAL CRASH]: {ex.Message} ===");
                _logger.LogCritical(ex, "💥 Kafka Consumer crashed during connection initialization.");
            }
        }, stoppingToken);

        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        try
        {
            _kafkaConsumer?.Close();
            _kafkaConsumer?.Dispose();
        }
        catch { /* Suppress disposal errors if it never initialized */ }

        base.Dispose();
    }
}