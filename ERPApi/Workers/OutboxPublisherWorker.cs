using ERPApi.DBContext;
using Entities.Model;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ERPApi.Workers
{
    /// <summary>
    /// Background service that polls the outbox table and publishes messages to RabbitMQ.
    /// Implements the Transactional Outbox pattern for guaranteed delivery.
    /// </summary>
    public class OutboxPublisherWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OutboxPublisherWorker> _logger;
        private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);
        private readonly int _batchSize = 10;
        private readonly int _maxRetries = 3;

        public OutboxPublisherWorker(
            IServiceProvider serviceProvider,
            ILogger<OutboxPublisherWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OutboxPublisher started. Polling every {PollInterval} seconds", _pollInterval.TotalSeconds);

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

                await Task.Delay(_pollInterval, stoppingToken);
            }

            _logger.LogInformation("OutboxPublisher stopped");
        }

        private async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

            // Get unprocessed messages ordered by creation time
            var unpublishedMessages = await context.OutboxMessages
                .Where(o => o.ProcessedAt == null && o.RetryCount < _maxRetries)
                .OrderBy(o => o.CreatedAt)
                .Take(_batchSize)
                .ToListAsync(cancellationToken);

            if (!unpublishedMessages.Any())
                return;

            _logger.LogInformation("Processing {Count} outbox messages", unpublishedMessages.Count);

            foreach (var message in unpublishedMessages)
            {
                try
                {
                    // Deserialize and publish the message
                    var eventObject = DeserializeMessage(message.MessageType, message.Payload);
                    
                    await publishEndpoint.Publish(eventObject, eventObject.GetType(), cancellationToken);

                    // Mark as processed
                    message.ProcessedAt = DateTime.UtcNow;
                    await context.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation(
                        "Published outbox message {MessageId} of type {MessageType}", 
                        message.Id, message.MessageType);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, 
                        "Failed to publish outbox message {MessageId} of type {MessageType}. Retry count: {RetryCount}", 
                        message.Id, message.MessageType, message.RetryCount);

                    // Increment retry count
                    message.RetryCount++;
                    await context.SaveChangesAsync(cancellationToken);

                    if (message.RetryCount >= _maxRetries)
                    {
                        _logger.LogError(
                            "Outbox message {MessageId} exceeded max retries ({MaxRetries}). Moving to dead letter.", 
                            message.Id, _maxRetries);
                    }
                }
            }
        }

        private object DeserializeMessage(string messageType, string payload)
        {
            // Map message type to actual type
            var type = messageType switch
            {
                "OrderCreated" => typeof(Contracts.Events.OrderCreated),
                _ => throw new InvalidOperationException($"Unknown message type: {messageType}")
            };

            var message = JsonSerializer.Deserialize(payload, type);
            if (message == null)
                throw new InvalidOperationException($"Failed to deserialize message of type {messageType}");

            return message;
        }
    }
}
