using Contracts.Events;
using MassTransit;

namespace ERPApi.Consumers
{
    /// <summary>
    /// Consumes OrderInvalid events and logs them for manual review.
    /// Invalid orders are logged to Seq for investigation.
    /// </summary>
    public class OrderInvalidConsumer : IConsumer<OrderInvalid>
    {
        private readonly ILogger<OrderInvalidConsumer> _logger;

        public OrderInvalidConsumer(ILogger<OrderInvalidConsumer> logger)
        {
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

            // Note: Invalid orders are automatically logged by Serilog and available in Seq
            // MassTransit also stores failed messages in dead letter queue
            
            // TODO: Send notification to operations team
            // TODO: Create ticket in support system
            
            await Task.CompletedTask;
        }
    }
}
