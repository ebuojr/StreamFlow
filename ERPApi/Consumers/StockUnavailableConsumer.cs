using Contracts.Events;
using ERPApi.DBContext;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERPApi.Consumers
{
    /// <summary>
    /// Consumes StockUnavailable events and updates order state to terminal failed state.
    /// Event-driven state update (no HTTP calls).
    /// </summary>
    public class StockUnavailableConsumer : IConsumer<StockUnavailable>
    {
        private readonly OrderDbContext _context;
        private readonly ILogger<StockUnavailableConsumer> _logger;

        public StockUnavailableConsumer(OrderDbContext context, ILogger<StockUnavailableConsumer> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<StockUnavailable> context)
        {
            var message = context.Message;
            
            _logger.LogWarning("Received StockUnavailable event for Order {OrderId}. Unavailable SKUs: {UnavailableSkus} (CorrelationId: {CorrelationId})",
                message.OrderId, string.Join(", ", message.UnavailableSkus), message.CorrelationId);

            try
            {
                var order = await _context.Orders
                    .Include(o => o.OrderItems)
                    .FirstOrDefaultAsync(o => o.Id == message.OrderId);

                if (order == null)
                {
                    _logger.LogWarning("Order {OrderId} not found when processing StockUnavailable event (CorrelationId: {CorrelationId})",
                        message.OrderId, message.CorrelationId);
                    return;
                }

                // Update order state
                order.OrderState = "StockUnavailable";
                
                // Mark all items as Unavailable since no stock is available
                foreach (var item in order.OrderItems)
                {
                    item.Status = "Unavailable";
                }
                
                _context.Orders.Update(order);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated Order {OrderId} state to StockUnavailable, all {ItemCount} items marked as Unavailable (CorrelationId: {CorrelationId}). Reason: {Reason}",
                    message.OrderId, order.OrderItems.Count, message.CorrelationId, message.Reason);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating order state for Order {OrderId} (CorrelationId: {CorrelationId})",
                    message.OrderId, message.CorrelationId);
                throw;
            }
        }
    }
}
