using Contracts.Events;
using ERPApi.DBContext;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERPApi.Consumers
{
    /// <summary>
    /// Consumes OrderPacked events and updates order state to final completed state.
    /// Event-driven state update (no HTTP calls).
    /// </summary>
    public class OrderPackedConsumer : IConsumer<OrderPacked>
    {
        private readonly OrderDbContext _context;
        private readonly ILogger<OrderPackedConsumer> _logger;

        public OrderPackedConsumer(OrderDbContext context, ILogger<OrderPackedConsumer> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<OrderPacked> context)
        {
            var message = context.Message;
            
            _logger.LogInformation("Received OrderPacked event for Order {OrderId} (CorrelationId: {CorrelationId})",
                message.OrderId, message.CorrelationId);

            try
            {
                var order = await _context.Orders
                    .Include(o => o.OrderItems)
                    .FirstOrDefaultAsync(o => o.Id == message.OrderId);

                if (order == null)
                {
                    _logger.LogWarning("Order {OrderId} not found when processing OrderPacked event (CorrelationId: {CorrelationId})",
                        message.OrderId, message.CorrelationId);
                    return;
                }

                // Update order state
                order.OrderState = "Packed";
                
                // Create lookup set for packed SKUs
                var packedSkus = new HashSet<string>(message.Items.Select(i => i.Sku ?? string.Empty));
                
                // Mark packed items as "Packed" (final state)
                foreach (var item in order.OrderItems)
                {
                    if (packedSkus.Contains(item.Sku ?? string.Empty))
                    {
                        item.Status = "Packed";
                    }
                    // Note: Items not in the packed list remain in their previous state (e.g., "Unavailable" for partial orders)
                }
                
                _context.Orders.Update(order);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated Order {OrderId} state to Packed (final state), {PackedCount} items marked as Packed (CorrelationId: {CorrelationId})",
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
