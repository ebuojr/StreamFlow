using Contracts.Events;
using ERPApi.DBContext;
using MassTransit;

namespace ERPApi.Consumers
{
    /// <summary>
    /// Consumes OrderInvalid events and stores them for manual review.
    /// Invalid orders are logged and stored in database for investigation.
    /// </summary>
    public class OrderInvalidConsumer : IConsumer<OrderInvalid>
    {
        private readonly OrderDbContext _context;
        private readonly ILogger<OrderInvalidConsumer> _logger;

        public OrderInvalidConsumer(OrderDbContext context, ILogger<OrderInvalidConsumer> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<OrderInvalid> context)
        {
            var message = context.Message;
            
            _logger.LogWarning(
                "⚠️ [INVALID ORDER] Received invalid order {OrderId}. Reason: {Reason}. Errors: {Errors} (CorrelationId: {CorrelationId})",
                message.OrderId,
                message.Reason,
                string.Join(", ", message.ValidationErrors),
                message.CorrelationId);

            try
            {
                // Store in Outbox table for manual review
                var outboxRecord = new Entities.Model.Outbox
                {
                    Id = Guid.NewGuid(),
                    MessageType = "OrderInvalid",
                    Payload = System.Text.Json.JsonSerializer.Serialize(message),
                    CreatedAt = DateTime.UtcNow,
                    ProcessedAt = null, // Mark as unprocessed for manual review
                    RetryCount = 888 // Special marker for invalid orders
                };

                _context.OutboxMessages.Add(outboxRecord);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Stored invalid order {OrderId} in database for manual review. Outbox ID: {OutboxId} (CorrelationId: {CorrelationId})",
                    message.OrderId,
                    outboxRecord.Id,
                    message.CorrelationId);

                // TODO: Send notification to operations team
                // TODO: Create ticket in support system
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to store invalid order {OrderId} in database (CorrelationId: {CorrelationId})",
                    message.OrderId,
                    message.CorrelationId);
                throw;
            }
        }
    }
}
