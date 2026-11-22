using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniOps.Application.Commands;
using OmniOps.Core.Entities;
using OmniOps.Core.Exceptions;
using OmniOps.Infrastructure.Configuration;

namespace OmniOps.Infrastructure.BackgroundWorkers;

/// <summary>
/// Runs one telemetry packet through <see cref="ProcessTelemetryCommand"/> in a fresh DI scope per attempt,
/// retrying transient failures (the handler releases its dedup lock before throwing them).
/// Validation failures and exhausted retries go to <c>rejectAsync</c>.
/// </summary>
public class TelemetryMessageProcessor
{
    private readonly ILogger<TelemetryMessageProcessor> _logger;
    private readonly IngestionOptions _options;

    public TelemetryMessageProcessor(
        ILogger<TelemetryMessageProcessor> logger,
        IOptions<IngestionOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public async Task ProcessAsync(
        string rawPayload,
        VehicleTelemetry telemetry,
        IServiceScopeFactory scopeFactory,
        Func<string, string, CancellationToken, Task> rejectAsync,
        CancellationToken cancellationToken)
    {
        var maxAttempts = Math.Max(1, _options.ProcessingRetryMaxAttempts);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var scope = scopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            try
            {
                await mediator.Send(new ProcessTelemetryCommand(telemetry), cancellationToken);
                return;
            }
            catch (ValidationException validationEx)
            {
                _logger.LogWarning(validationEx, "Telemetry validation failed; rejecting packet");
                await rejectAsync(rawPayload, validationEx.Message, cancellationToken);
                return;
            }
            catch (TransientProcessingException tex) when (attempt < maxAttempts)
            {
                _logger.LogWarning(tex,
                    "Transient telemetry processing failure on attempt {Attempt}/{MaxAttempts}. " +
                    "Dedup lock was released; retrying after {DelayMs}ms",
                    attempt,
                    maxAttempts,
                    _options.ProcessingRetryDelayMilliseconds);

                await Task.Delay(
                    TimeSpan.FromMilliseconds(_options.ProcessingRetryDelayMilliseconds),
                    cancellationToken);
            }
            catch (TransientProcessingException tex)
            {
                var reason = $"transient failure exhausted retries ({maxAttempts}): {tex.Message}";
                _logger.LogError(tex,
                    "Transient telemetry processing failed after {MaxAttempts} attempts; rejecting packet",
                    maxAttempts);
                await rejectAsync(rawPayload, reason, cancellationToken);
                return;
            }
        }
    }
}
