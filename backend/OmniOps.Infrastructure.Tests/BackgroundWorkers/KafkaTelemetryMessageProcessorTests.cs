using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using OmniOps.Application.Commands;
using OmniOps.Core.Entities;
using OmniOps.Core.Exceptions;
using OmniOps.Infrastructure.BackgroundWorkers;
using OmniOps.Infrastructure.Configuration;

namespace OmniOps.Infrastructure.Tests.BackgroundWorkers;

public class KafkaTelemetryMessageProcessorTests
{
    private static KafkaTelemetryMessageProcessor CreateProcessor(
        int maxAttempts = 3,
        int delayMs = 0) =>
        new(
            NullLogger<KafkaTelemetryMessageProcessor>.Instance,
            Options.Create(new KafkaOptions
            {
                ProcessingRetryMaxAttempts = maxAttempts,
                ProcessingRetryDelayMilliseconds = delayMs,
                DlqTopic = "fleet-telemetry-dlq"
            }));

    private static (IServiceScopeFactory ScopeFactory, IMediator Mediator) CreateScopeFactory(IMediator mediator)
    {
        var scope = Substitute.For<IServiceScope>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        scope.ServiceProvider.Returns(serviceProvider);
        serviceProvider.GetService(typeof(IMediator)).Returns(mediator);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);
        return (scopeFactory, mediator);
    }

    private static VehicleTelemetry CreateTelemetry() => new()
    {
        Id = Guid.NewGuid(),
        VehicleId = "Truck-001",
        Latitude = 41.99,
        Longitude = 21.43,
        Speed = 80,
        FuelLevel = 75,
        EngineTemperature = 90,
        Timestamp = DateTime.UtcNow
    };

    [Fact]
    public async Task ProcessAsync_WhenTransientFailureThenSuccess_DoesNotSendToDlq()
    {
        var processor = CreateProcessor(maxAttempts: 3, delayMs: 0);
        var mediator = Substitute.For<IMediator>();
        var (scopeFactory, _) = CreateScopeFactory(mediator);
        var telemetry = CreateTelemetry();
        var sendAttempts = 0;
        var dlqCalls = 0;

        mediator.Send(Arg.Any<ProcessTelemetryCommand>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                sendAttempts++;
                return sendAttempts switch
                {
                    1 => Task.FromException<bool>(new TransientProcessingException(
                        "db down", new InvalidOperationException("db down"))),
                    _ => Task.FromResult(true)
                };
            });

        await processor.ProcessAsync(
            "{\"vehicleId\":\"Truck-001\"}",
            telemetry,
            scopeFactory,
            (_, _, _) =>
            {
                dlqCalls++;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.Equal(0, dlqCalls);
        await mediator.Received(2).Send(Arg.Any<ProcessTelemetryCommand>(), Arg.Any<CancellationToken>());
        scopeFactory.Received(2).CreateScope();
    }

    [Fact]
    public async Task ProcessAsync_WhenTransientFailureExhaustedRetries_SendsToDlq()
    {
        var processor = CreateProcessor(maxAttempts: 3, delayMs: 0);
        var mediator = Substitute.For<IMediator>();
        var (scopeFactory, _) = CreateScopeFactory(mediator);
        var telemetry = CreateTelemetry();
        string? dlqReason = null;

        mediator.Send(Arg.Any<ProcessTelemetryCommand>(), Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(_ => throw new TransientProcessingException(
                "db down", new InvalidOperationException("db down")));

        await processor.ProcessAsync(
            "{\"vehicleId\":\"Truck-001\"}",
            telemetry,
            scopeFactory,
            (_, reason, _) =>
            {
                dlqReason = reason;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.NotNull(dlqReason);
        Assert.Contains("transient failure exhausted retries", dlqReason, StringComparison.OrdinalIgnoreCase);
        await mediator.Received(3).Send(Arg.Any<ProcessTelemetryCommand>(), Arg.Any<CancellationToken>());
        scopeFactory.Received(3).CreateScope();
    }

    [Fact]
    public async Task ProcessAsync_WhenValidationFails_SendsToDlqImmediatelyWithoutRetry()
    {
        var processor = CreateProcessor(maxAttempts: 3, delayMs: 0);
        var mediator = Substitute.For<IMediator>();
        var (scopeFactory, _) = CreateScopeFactory(mediator);
        var telemetry = CreateTelemetry();
        var dlqCalls = 0;

        mediator.Send(Arg.Any<ProcessTelemetryCommand>(), Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(_ => throw new ValidationException([
                new ValidationFailure("VehicleId", "VehicleId is required")
            ]));

        await processor.ProcessAsync(
            "{\"vehicleId\":\"\"}",
            telemetry,
            scopeFactory,
            (_, _, _) =>
            {
                dlqCalls++;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.Equal(1, dlqCalls);
        await mediator.Received(1).Send(Arg.Any<ProcessTelemetryCommand>(), Arg.Any<CancellationToken>());
        scopeFactory.Received(1).CreateScope();
    }
}
