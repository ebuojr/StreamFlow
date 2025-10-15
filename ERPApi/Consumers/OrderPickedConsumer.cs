using Contracts.Events;
using ERPApi.DBContext;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERPApi.Consumers
{
    /// <summary>
    /// Consumes OrderPicked events and updates order state in database.
    /// Event-driven state update (no HTTP calls).
    /// </summary>
    public class OrderPickedConsumer : IConsumer<OrderPicked>
    {
        private readonly OrderDbContext _context;
        private readonly ILogger<OrderPickedConsumer> _logger;

        public OrderPickedConsumer(OrderDbContext context, ILogger<OrderPickedConsumer> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<OrderPicked> context)
        {
            var message = context.Message;
            
            _logger.LogInformation("Received OrderPicked event for Order {OrderId} (CorrelationId: {CorrelationId})",
                message.OrderId, message.CorrelationId);

            try
            {
                var order = await _context.Orders
                    .Include(o => o.OrderItems)
                    .FirstOrDefaultAsync(o => o.Id == message.OrderId);

                if (order == null)
                {
                    _logger.LogWarning("Order {OrderId} not found when processing OrderPicked event (CorrelationId: {CorrelationId})",
                        message.OrderId, message.CorrelationId);
                    return;
                }

                // Update order state
                order.OrderState = "Picked";
                
                // Create lookup set for picked SKUs
                var pickedSkus = new HashSet<string>(message.Items.Select(i => i.Sku ?? string.Empty));
                
                // Mark picked items as "Picked" (items in the event were actually picked)
                foreach (var item in order.OrderItems)
                {
                    if (pickedSkus.Contains(item.Sku ?? string.Empty))
                    {
                        item.Status = "Picked";
                    }
                    // Note: Items not in the picked list remain in their previous state (e.g., "Unavailable" for partial orders)
                }
                
                _context.Orders.Update(order);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated Order {OrderId} state to Picked, {PickedCount} items marked as Picked (CorrelationId: {CorrelationId})",
                    message.OrderId, message.Items.Count, message.CorrelationId);
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
