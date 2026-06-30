using System;
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
    private IConsumer<Ignore, string>? _kafkaConsumer;

    public KafkaTelemetryConsumer(
        ILogger<KafkaTelemetryConsumer> logger, 
        IServiceProvider serviceProvider,
        IOptions<KafkaOptions> kafkaOptions)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;

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

                        _logger.LogInformation("📥 Message received from Kafka: {Message}", consumeResult.Message.Value);

                        var telemetry = JsonSerializer.Deserialize<VehicleTelemetry>(consumeResult.Message.Value, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (telemetry != null)
                        {
                            _logger.LogInformation("Handing off telemetry for vehicle {VehicleId} to MediatR", telemetry.VehicleId);
                            using var scope = _serviceProvider.CreateScope();
                            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                            await mediator.Send(new ProcessTelemetryCommand(telemetry), stoppingToken);
                        }
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