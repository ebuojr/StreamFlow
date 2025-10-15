using Contracts.Events;
using ERPApi.DBContext;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERPApi.Consumers
{
    /// <summary>
    /// Consumes PartialStockReserved events when only some order items are available.
    /// Updates order state to 'PartialDelivered' and logs unavailable items.
    /// </summary>
    public class PartialStockReservedConsumer : IConsumer<PartialStockReserved>
    {
        private readonly OrderDbContext _context;
        private readonly ILogger<PartialStockReservedConsumer> _logger;

        public PartialStockReservedConsumer(OrderDbContext context, ILogger<PartialStockReservedConsumer> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<PartialStockReserved> context)
        {
            var message = context.Message;
            
            var unavailableSkusLog = string.Join(", ", message.UnavailableItems.Select(i => $"{i.Sku} (Qty: {i.Quantity})"));
            var availableSkusLog = string.Join(", ", message.AvailableItems.Select(i => $"{i.Sku} (Qty: {i.Quantity})"));
            
            _logger.LogWarning(
                "⚠️ [PARTIAL FULFILLMENT] Order {OrderNo} has partial stock availability. " +
                "Available items: [{AvailableItems}], Unavailable items: [{UnavailableItems}] (CorrelationId: {CorrelationId})",
                message.OrderNo, availableSkusLog, unavailableSkusLog, message.CorrelationId);

            try
            {
                var order = await _context.Orders
                    .Include(o => o.OrderItems)
                    .FirstOrDefaultAsync(o => o.Id == message.OrderId);

                if (order == null)
                {
                    _logger.LogWarning("Order {OrderId} not found when processing PartialStockReserved event (CorrelationId: {CorrelationId})",
                        message.OrderId, message.CorrelationId);
                    return;
                }

                // Update order state
                order.OrderState = "PartialDelivered";
                
                // Create lookup sets for available and unavailable SKUs
                var availableSkus = new HashSet<string>(message.AvailableItems.Select(i => i.Sku ?? string.Empty));
                var unavailableSkus = new HashSet<string>(message.UnavailableItems.Select(i => i.Sku ?? string.Empty));
                
                // Update item status based on availability
                foreach (var item in order.OrderItems)
                {
                    if (availableSkus.Contains(item.Sku ?? string.Empty))
                    {
                        item.Status = "Available";
                    }
                    else if (unavailableSkus.Contains(item.Sku ?? string.Empty))
                    {
                        item.Status = "Unavailable";
                    }
                }
                
                _context.Orders.Update(order);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Updated Order {OrderNo} state to PartialDelivered. {AvailableCount} items marked Available, {UnavailableCount} items marked Unavailable (CorrelationId: {CorrelationId})",
                    message.OrderNo, message.AvailableItems.Count, message.UnavailableItems.Count, message.CorrelationId);
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
