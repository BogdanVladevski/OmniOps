using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniOps.Infrastructure.Configuration;
using OmniOps.Infrastructure.Data;

namespace OmniOps.Infrastructure.BackgroundWorkers
{
    public class OutboxPublisherWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OutboxPublisherWorker> _logger;
        private readonly string _targetTopic;

        public OutboxPublisherWorker(
            IServiceProvider serviceProvider,
            ILogger<OutboxPublisherWorker> logger,
            IOptions<KafkaOptions> kafkaOptions)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _targetTopic = kafkaOptions.Value.Topic;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OutboxPublisherWorker background service has started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessOutboxMessagesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing outbox messages.");
                }

                // Poll every 2 seconds
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("OutboxPublisherWorker background service has stopped.");
        }

        private async Task ProcessOutboxMessagesAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var producer = scope.ServiceProvider.GetRequiredService<IProducer<Null, string>>();

            // Fetch batch of unprocessed outbox messages
            var messages = await dbContext.OutboxMessages
                .Where(m => m.ProcessedOnUtc == null)
                .OrderBy(m => m.OccurredOnUtc)
                .Take(20)
                .ToListAsync(stoppingToken);

            if (!messages.Any())
            {
                return;
            }

            _logger.LogInformation("Found {Count} unprocessed outbox messages to publish.", messages.Count);

            foreach (var message in messages)
            {
                try
                {
                    // Publish the serialized content to Kafka
                    var deliveryResult = await producer.ProduceAsync(_targetTopic, new Message<Null, string>
                    {
                        Value = message.Content
                    }, stoppingToken);

                    if (deliveryResult.Status == PersistenceStatus.Persisted)
                    {
                        message.ProcessedOnUtc = DateTime.UtcNow;
                        message.Error = null;
                        _logger.LogInformation("Successfully published outbox message {Id} to Kafka topic {Topic}.", message.Id, _targetTopic);
                    }
                    else
                    {
                        message.Error = $"Failed to persist message in Kafka. Status: {deliveryResult.Status}";
                        _logger.LogWarning("Outbox message {Id} was not fully persisted: {Status}", message.Id, deliveryResult.Status);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish outbox message {Id} to Kafka.", message.Id);
                    message.Error = ex.Message;
                }
            }

            await dbContext.SaveChangesAsync(stoppingToken);
        }
    }
}
