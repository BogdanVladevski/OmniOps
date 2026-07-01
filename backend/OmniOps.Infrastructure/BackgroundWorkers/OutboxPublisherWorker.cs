using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Configuration;
using OmniOps.Infrastructure.Data;
namespace OmniOps.Infrastructure.BackgroundWorkers;

public class OutboxPublisherWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxPublisherWorker> _logger;
    private readonly string _eventsTopic;

    public OutboxPublisherWorker(
        IServiceProvider serviceProvider,
        ILogger<OutboxPublisherWorker> logger,
        IOptions<KafkaOptions> kafkaOptions)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _eventsTopic = kafkaOptions.Value.EventsTopic;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxPublisherWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox messages");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("OutboxPublisherWorker stopped");
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var producer = scope.ServiceProvider.GetRequiredService<IKafkaMessageProducer>();

        var messages = await dbContext.OutboxMessages
            .Where(m => m.ProcessedOnUtc == null)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(20)
            .ToListAsync(stoppingToken);

        if (messages.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Publishing {Count} outbox messages to {Topic}", messages.Count, _eventsTopic);

        foreach (var message in messages)
        {
            try
            {
                await producer.ProduceAsync(
                    _eventsTopic,
                    message.Content,
                    new Dictionary<string, string> { ["event-type"] = message.Type },
                    stoppingToken);

                message.ProcessedOnUtc = DateTime.UtcNow;
                message.Error = null;
                _logger.LogInformation(
                    "Published outbox message {OutboxId} to topic {Topic}",
                    message.Id, _eventsTopic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish outbox message {OutboxId}", message.Id);
                message.Error = ex.Message;
            }
        }

        await dbContext.SaveChangesAsync(stoppingToken);
    }
}
