using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Services;

namespace OmniOps.Infrastructure.BackgroundWorkers;

/// <summary>Drains <see cref="ChannelTelemetryIngestionQueue"/> through <see cref="TelemetryMessageProcessor"/>.</summary>
public class TelemetryIngestionWorker : BackgroundService
{
    private readonly ChannelTelemetryIngestionQueue _queue;
    private readonly TelemetryMessageProcessor _processor;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITelemetryMetrics _metrics;
    private readonly ILogger<TelemetryIngestionWorker> _logger;

    public TelemetryIngestionWorker(
        ChannelTelemetryIngestionQueue queue,
        TelemetryMessageProcessor processor,
        IServiceScopeFactory scopeFactory,
        ITelemetryMetrics metrics,
        ILogger<TelemetryIngestionWorker> logger)
    {
        _queue = queue;
        _processor = processor;
        _scopeFactory = scopeFactory;
        _metrics = metrics;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Telemetry ingestion worker started");

        try
        {
            await foreach (var telemetry in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await _processor.ProcessAsync(
                        JsonSerializer.Serialize(telemetry),
                        telemetry,
                        _scopeFactory,
                        RejectAsync,
                        stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Never let one bad packet stop ingestion for everyone else.
                    _logger.LogError(ex, "Telemetry processing failed for vehicle {VehicleId}", telemetry.VehicleId);
                    _metrics.RecordDlqRouted(ex.Message);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }

        _logger.LogInformation("Telemetry ingestion worker stopped");
    }

    private Task RejectAsync(string rawPayload, string reason, CancellationToken cancellationToken)
    {
        _logger.LogError("Telemetry packet rejected: {Reason}. Payload: {Payload}", reason, rawPayload);
        _metrics.RecordDlqRouted(reason);
        return Task.CompletedTask;
    }
}
