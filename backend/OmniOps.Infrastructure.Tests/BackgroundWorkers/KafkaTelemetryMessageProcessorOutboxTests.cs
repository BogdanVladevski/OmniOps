using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using OmniOps.Application.Commands;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.BackgroundWorkers;
using OmniOps.Infrastructure.Configuration;
using OmniOps.Infrastructure.Data;
using OmniOps.Infrastructure.Data.Interceptors;
using OmniOps.Infrastructure.Services;

namespace OmniOps.Infrastructure.Tests.BackgroundWorkers;

public class KafkaTelemetryMessageProcessorOutboxTests
{
    [Fact]
    public async Task ProcessAsync_WithFreshScopePerRetry_PersistsExactlyOneOutboxMessage()
    {
        ThrowOnFirstSaveChangesInterceptor.ResetForTests();

        var databaseName = Guid.NewGuid().ToString();
        var services = BuildServices(databaseName);
        await using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var processor = new KafkaTelemetryMessageProcessor(
            NullLogger<KafkaTelemetryMessageProcessor>.Instance,
            Options.Create(new KafkaOptions
            {
                ProcessingRetryMaxAttempts = 3,
                ProcessingRetryDelayMilliseconds = 0,
                DlqTopic = "fleet-telemetry-dlq"
            }));

        var telemetry = new VehicleTelemetry
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

        await processor.ProcessAsync(
            "{\"vehicleId\":\"Truck-001\"}",
            telemetry,
            scopeFactory,
            (_, _, _) => Task.CompletedTask,
            CancellationToken.None);

        using var verifyScope = scopeFactory.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await verifyContext.OutboxMessages.CountAsync());
        Assert.Equal(1, await verifyContext.Telemetries.CountAsync());
    }

    private static ServiceCollection BuildServices(string databaseName)
    {
        var deduplication = Substitute.For<IDeduplicationService>();
        deduplication.TryAcquireProcessingLockAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var services = new ServiceCollection();

        services.AddDbContext<AppDbContext>((_, options) =>
        {
            options.UseInMemoryDatabase(databaseName);
            options.AddInterceptors(
                new OutboxSaveChangesInterceptor(),
                new ThrowOnFirstSaveChangesInterceptor());
        });

        services.AddScoped<ITelemetryRepository, TelemetryRepository>();
        services.AddSingleton(deduplication);
        services.AddSingleton(Substitute.For<ITelemetryCacheService>());
        services.AddSingleton(Substitute.For<ITelemetryBroadcastService>());
        services.AddSingleton(Substitute.For<IAnomalyDetectionService>());
        services.AddSingleton(Substitute.For<IPlaybookOrchestrationService>());
        services.AddSingleton(Substitute.For<IShipmentRepository>());
        services.AddSingleton(Substitute.For<ITelemetryMetrics>());
        services.AddLogging();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ProcessTelemetryCommand).Assembly));

        return services;
    }
}
