using Contracts.Events;
using ERPApi.DBContext;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERPApi.Consumers
{
    /// <summary>
    /// Consumes InvalidOrder events for tracking and alerting.
    /// Logs validation failures and stores them for manual review.
    /// </summary>
    public class InvalidOrderConsumer : IConsumer<InvalidOrder>
    {
        private readonly OrderDbContext _context;
        private readonly ILogger<InvalidOrderConsumer> _logger;

        public InvalidOrderConsumer(OrderDbContext context, ILogger<InvalidOrderConsumer> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<InvalidOrder> context)
        {
            var message = context.Message;
            
            _logger.LogWarning("⚠️ [INVALID ORDER] Order {OrderId} validation failed. Reason: {Reason} (CorrelationId: {CorrelationId})",
                message.OrderId, message.Reason, message.CorrelationId);

            _logger.LogWarning("Validation Errors: {Errors}", string.Join(", ", message.ValidationErrors));

            try
            {
                // Mark order as Failed if it exists
                var order = await _context.Orders
                    .FirstOrDefaultAsync(o => o.Id == message.OrderId);

                if (order != null)
                {
                    order.OrderState = "Failed";
                    _context.Orders.Update(order);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Updated Order {OrderId} state to Failed (CorrelationId: {CorrelationId})",
                        message.OrderId, message.CorrelationId);
                }

                // TODO: Send alert to operations team (email, Slack, etc.)
                // TODO: Store in separate InvalidOrders table for manual review
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing InvalidOrder event for Order {OrderId} (CorrelationId: {CorrelationId})",
                    message.OrderId, message.CorrelationId);
                throw;
            }
        }
    }
}
