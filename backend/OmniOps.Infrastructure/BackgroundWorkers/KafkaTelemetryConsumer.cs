using System.Diagnostics;
using System.Text.Json;
using Confluent.Kafka;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniOps.Application.Commands;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Configuration;
using OmniOps.Infrastructure.Parsing;
namespace OmniOps.Infrastructure.BackgroundWorkers;

public class KafkaTelemetryConsumer : BackgroundService
{
    private readonly ILogger<KafkaTelemetryConsumer> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConsumerConfig _config;
    private readonly string _topic;
    private readonly string _dlqTopic;
    private readonly IKafkaMessageProducer _kafkaProducer;
    private readonly ITelemetryMetrics _metrics;
    private IConsumer<Ignore, string>? _kafkaConsumer;

    private static readonly ActivitySource ActivitySource = new("OmniOps.Infrastructure.TelemetryConsumer");

    public KafkaTelemetryConsumer(
        ILogger<KafkaTelemetryConsumer> logger,
        IServiceProvider serviceProvider,
        IOptions<KafkaOptions> kafkaOptions,
        IKafkaMessageProducer kafkaProducer,
        ITelemetryMetrics metrics)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _kafkaProducer = kafkaProducer;
        _metrics = metrics;

        var options = kafkaOptions.Value;
        _config = new ConsumerConfig
        {
            BootstrapServers = options.BootstrapServers,
            GroupId = options.GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };
        _topic = options.Topic;
        _dlqTopic = options.DlqTopic;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("KafkaTelemetryConsumer starting");

        await Task.Yield();

        var retryDelay = TimeSpan.FromSeconds(2);
        var maxRetryDelay = TimeSpan.FromMinutes(2);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation(
                    "Connecting Kafka consumer to {BootstrapServers}",
                    _config.BootstrapServers);

                _kafkaConsumer = new ConsumerBuilder<Ignore, string>(_config).Build();
                _kafkaConsumer.Subscribe(_topic);

                _logger.LogInformation("Kafka consumer subscribed to topic {Topic}", _topic);
                retryDelay = TimeSpan.FromSeconds(2);

                while (!stoppingToken.IsCancellationRequested)
                {
                    ConsumeResult<Ignore, string>? consumeResult = null;
                    try
                    {
                        consumeResult = _kafkaConsumer.Consume(stoppingToken);
                        if (consumeResult is null)
                        {
                            continue;
                        }

                        string? traceParent = null;
                        if (consumeResult.Message.Headers?.TryGetLastBytes("traceparent", out var headerBytes) == true)
                        {
                            traceParent = System.Text.Encoding.UTF8.GetString(headerBytes);
                        }

                        using var activity = ActivitySource.StartActivity(
                            "KafkaConsumeTelemetry",
                            ActivityKind.Consumer,
                            traceParent ?? string.Empty);
                        activity?.SetTag("messaging.system", "kafka");
                        activity?.SetTag("messaging.destination.name", _topic);

                        VehicleTelemetry? telemetry;
                        try
                        {
                            telemetry = TelemetryPayloadParser.TryParse(consumeResult.Message.Value);

                            if (!TelemetryPayloadParser.IsValidTelemetry(telemetry))
                            {
                                throw new JsonException(
                                    telemetry is null ? "Deserialized telemetry was null" : "VehicleId is required");
                            }
                        }
                        catch (Exception parseEx)
                        {
                            _logger.LogError(parseEx,
                                "Telemetry deserialization failed. Routing to DLQ topic {DlqTopic}",
                                _dlqTopic);
                            await SendToDlqAsync(consumeResult.Message.Value, parseEx.Message, stoppingToken);
                            continue;
                        }

                        using var scope = _serviceProvider.CreateScope();
                        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                        try
                        {
                            await mediator.Send(new ProcessTelemetryCommand(telemetry!), stoppingToken);
                        }
                        catch (ValidationException validationEx)
                        {
                            _logger.LogWarning(validationEx,
                                "Telemetry validation failed. Routing to DLQ topic {DlqTopic}",
                                _dlqTopic);
                            await SendToDlqAsync(consumeResult.Message.Value, validationEx.Message, stoppingToken);
                        }
                    }
                    catch (ConsumeException ex)
                    {
                        _logger.LogError(ex, "Kafka consume error");
                        if (ex.Error.IsFatal)
                        {
                            break;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        // With EnableAutoCommit=true, unhandled processing errors would advance the offset
                        // and permanently drop the packet unless we route it to the DLQ explicitly.
                        _logger.LogError(ex,
                            "Telemetry processing failed. Routing to DLQ topic {DlqTopic}",
                            _dlqTopic);

                        if (consumeResult?.Message?.Value is not null)
                        {
                            await SendToDlqAsync(consumeResult.Message.Value, ex.Message, stoppingToken);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Kafka consumer connection failed. Retrying in {DelaySeconds}s",
                    retryDelay.TotalSeconds);

                CleanupConsumer();

                try
                {
                    await Task.Delay(retryDelay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                retryDelay = TimeSpan.FromMilliseconds(
                    Math.Min(retryDelay.TotalMilliseconds * 2, maxRetryDelay.TotalMilliseconds));
            }
            finally
            {
                CleanupConsumer();
            }
        }

        _logger.LogInformation("KafkaTelemetryConsumer stopped");
    }

    private async Task SendToDlqAsync(string rawPayload, string reason, CancellationToken cancellationToken)
    {
        try
        {
            await _kafkaProducer.ProduceAsync(
                _dlqTopic,
                rawPayload,
                new Dictionary<string, string>
                {
                    ["dlq-reason"] = reason,
                    ["dlq-timestamp"] = DateTime.UtcNow.ToString("O")
                },
                cancellationToken);

            _metrics.RecordDlqRouted(reason);
            _logger.LogInformation("Poison message routed to DLQ topic {DlqTopic}", _dlqTopic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to route poison message to DLQ topic {DlqTopic}", _dlqTopic);
        }
    }

    private void CleanupConsumer()
    {
        try
        {
            _kafkaConsumer?.Close();
            _kafkaConsumer?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error disposing Kafka consumer");
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
