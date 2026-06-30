using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniOps.Core.Entities;
using OmniOps.Core.Telemetry;
using OmniOps.Infrastructure.Configuration;

namespace OmniOps.Infrastructure.BackgroundWorkers;

public class KafkaTelemetryConsumer : BackgroundService
{
    private readonly ILogger<KafkaTelemetryConsumer> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConsumerConfig _config;
    private readonly string _topic;
    private readonly IProducer<Null, string> _producer;
    private IConsumer<Ignore, string>? _kafkaConsumer;

    private static readonly ActivitySource _activitySource = new("OmniOps.Infrastructure.TelemetryConsumer");

    public KafkaTelemetryConsumer(
        ILogger<KafkaTelemetryConsumer> logger, 
        IServiceProvider serviceProvider,
        IOptions<KafkaOptions> kafkaOptions,
        IProducer<Null, string> producer)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _producer = producer;

        var options = kafkaOptions.Value;
        _config = new ConsumerConfig
        {
            BootstrapServers = options.BootstrapServers,
            GroupId = options.GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };
        _topic = options.Topic;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("KafkaTelemetryConsumer.ExecuteAsync starting background task.");

        // Yield execution immediately so that host startup isn't blocked by the Kafka consumer loop
        await Task.Yield();

        var retryDelay = TimeSpan.FromSeconds(2);
        var maxRetryDelay = TimeSpan.FromMinutes(2);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Attempting to connect Kafka Consumer to bootstrap servers: {Servers}...", _config.BootstrapServers);
                
                _kafkaConsumer = new ConsumerBuilder<Ignore, string>(_config).Build();
                _kafkaConsumer.Subscribe(_topic);

                _logger.LogInformation("🚀 Kafka Telemetry Consumer successfully subscribed to topic: {Topic}", _topic);
                
                // Reset connection retry delay on success
                retryDelay = TimeSpan.FromSeconds(2);

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        _logger.LogDebug("Waiting/Polling for message from Kafka...");
                        
                        // Blocking consume call with token
                        var consumeResult = _kafkaConsumer.Consume(stoppingToken);
                        if (consumeResult == null) continue;

                        // 1. Extract W3C trace context header (traceparent)
                        string? traceParent = null;
                        if (consumeResult.Message.Headers != null && 
                            consumeResult.Message.Headers.TryGetLastBytes("traceparent", out var headerBytes))
                        {
                            traceParent = System.Text.Encoding.UTF8.GetString(headerBytes);
                        }

                        // 2. Start a linked activity for Distributed Tracing
                        using var activity = _activitySource.StartActivity("KafkaConsumeTelemetry", ActivityKind.Consumer, traceParent ?? string.Empty);
                        activity?.SetTag("messaging.system", "kafka");
                        activity?.SetTag("messaging.destination.name", _topic);

                        _logger.LogInformation("📥 Message received from Kafka: {Message}", consumeResult.Message.Value);

                        VehicleTelemetry? telemetry = null;
                        try
                        {
                            telemetry = JsonSerializer.Deserialize<VehicleTelemetry>(consumeResult.Message.Value, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });
                        }
                        catch (Exception parseEx)
                        {
                            _logger.LogError(parseEx, "Failed to deserialize telemetry message. Sending to DLQ. Raw payload: {Payload}", consumeResult.Message.Value);
                            await SendToDlqAsync(consumeResult.Message.Value, parseEx.Message, stoppingToken);
                            continue;
                        }

                        if (telemetry == null)
                        {
                            _logger.LogWarning("Deserialized telemetry was null. Sending to DLQ. Raw payload: {Payload}", consumeResult.Message.Value);
                            await SendToDlqAsync(consumeResult.Message.Value, "Deserialized object was null", stoppingToken);
                            continue;
                        }

                        _logger.LogInformation("Handing off telemetry for vehicle {VehicleId} to MediatR", telemetry.VehicleId);
                        using var scope = _serviceProvider.CreateScope();
                        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                        await mediator.Send(new ProcessTelemetryCommand(telemetry), stoppingToken);
                    }
                    catch (ConsumeException ex)
                    {
                        _logger.LogError(ex, "Kafka consume error occurred.");
                        if (ex.Error.IsFatal)
                        {
                            _logger.LogWarning("Fatal Kafka ConsumeException encountered. Re-initializing consumer...");
                            break; // break inner loop to re-initialize consumer
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("Consumer loop canceled gracefully inside consume cycle.");
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error encountered while processing Kafka telemetry stream packet.");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Kafka Consumer initialization or connection failed. Retrying in {Delay} seconds...", retryDelay.TotalSeconds);
                
                CleanupConsumer();

                try
                {
                    await Task.Delay(retryDelay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                // Exponential backoff
                retryDelay = TimeSpan.FromMilliseconds(Math.Min(retryDelay.TotalMilliseconds * 2, maxRetryDelay.TotalMilliseconds));
            }
            finally
            {
                CleanupConsumer();
            }
        }

        _logger.LogInformation("KafkaTelemetryConsumer.ExecuteAsync has finished.");
    }

    private async Task SendToDlqAsync(string rawPayload, string reason, CancellationToken cancellationToken)
    {
        try
        {
            var dlqTopic = _topic + "-dlq";
            var headers = new Headers
            {
                { "dlq-reason", System.Text.Encoding.UTF8.GetBytes(reason) },
                { "dlq-timestamp", System.Text.Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("O")) }
            };

            _logger.LogInformation("Routing poison message to DLQ topic: {DlqTopic}", dlqTopic);
            await _producer.ProduceAsync(dlqTopic, new Message<Null, string>
            {
                Value = rawPayload,
                Headers = headers
            }, cancellationToken);
            
            _logger.LogInformation("Successfully routed poison message to DLQ.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send poison message to DLQ.");
        }
    }

    private void CleanupConsumer()
    {
        try
        {
            if (_kafkaConsumer != null)
            {
                _logger.LogInformation("Closing and disposing existing Kafka consumer instance.");
                _kafkaConsumer.Close();
                _kafkaConsumer.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to cleanly dispose Kafka consumer.");
        }
        finally
        {
            _kafkaConsumer = null;
        }
    }

    public override void Dispose()
    {
        CleanupConsumer();
        base.Dispose();
    }
}