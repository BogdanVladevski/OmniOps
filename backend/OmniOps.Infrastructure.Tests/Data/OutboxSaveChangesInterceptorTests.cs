using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OmniOps.Core.Entities;
using OmniOps.Core.Events;
using OmniOps.Infrastructure.Data;
using OmniOps.Infrastructure.Data.Interceptors;
using OmniOps.Infrastructure.Services;
using Testcontainers.PostgreSql;

namespace OmniOps.Infrastructure.Tests.Data;

public class OutboxSaveChangesInterceptorTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task SaveChanges_WithDomainEvent_PersistsOutboxMessageInSameTransaction()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .AddInterceptors(new OutboxSaveChangesInterceptor(new CorrelationContext()))
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

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
        telemetry.AddDomainEvent(new TelemetryReceivedEvent(telemetry));

        context.Telemetries.Add(telemetry);
        await context.SaveChangesAsync();

        Assert.Empty(telemetry.DomainEvents);

        var outboxMessage = await context.OutboxMessages.SingleAsync();
        Assert.Equal(nameof(TelemetryReceivedEvent), outboxMessage.Type);
        Assert.Contains("Truck-001", outboxMessage.Content);
        Assert.Null(outboxMessage.ProcessedOnUtc);

        var persistedTelemetry = await context.Telemetries.SingleAsync();
        Assert.Equal(telemetry.Id, persistedTelemetry.Id);
    }

    [Fact]
    public async Task SaveChanges_WhenTransactionRollsBack_DoesNotPersistOutboxOrTelemetry()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .AddInterceptors(new OutboxSaveChangesInterceptor(new CorrelationContext()))
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        await using var transaction = await context.Database.BeginTransactionAsync();

        var telemetry = new VehicleTelemetry
        {
            Id = Guid.NewGuid(),
            VehicleId = "Truck-rollback",
            Latitude = 41.0,
            Longitude = 21.0,
            Speed = 50,
            FuelLevel = 60,
            EngineTemperature = 80,
            Timestamp = DateTime.UtcNow
        };
        telemetry.AddDomainEvent(new TelemetryReceivedEvent(telemetry));
        context.Telemetries.Add(telemetry);
        await context.SaveChangesAsync();

        await transaction.RollbackAsync();

        Assert.Equal(0, await context.Telemetries.CountAsync());
        Assert.Equal(0, await context.OutboxMessages.CountAsync());
    }
}
