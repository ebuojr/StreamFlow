using Contracts.Events;
using ERPApi.DBContext;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERPApi.Consumers
{
    /// <summary>
    /// Consumes StockReserved events and updates order state in database.
    /// Event-driven state update (no HTTP calls).
    /// </summary>
    public class StockReservedConsumer : IConsumer<StockReserved>
    {
        private readonly OrderDbContext _context;
        private readonly ILogger<StockReservedConsumer> _logger;

        public StockReservedConsumer(OrderDbContext context, ILogger<StockReservedConsumer> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<StockReserved> context)
        {
            var message = context.Message;
            
            _logger.LogInformation("Received StockReserved event for Order {OrderId} | Partial: {IsPartial} ({Reserved}/{Requested}) (CorrelationId: {CorrelationId})",
                message.OrderId, message.IsPartialReservation, message.TotalReserved, message.TotalRequested, message.CorrelationId);

            try
            {
                var order = await _context.Orders
                    .Include(o => o.OrderItems)
                    .FirstOrDefaultAsync(o => o.Id == message.OrderId);

                if (order == null)
                {
                    _logger.LogWarning("Order {OrderId} not found when processing StockReserved event (CorrelationId: {CorrelationId})",
                        message.OrderId, message.CorrelationId);
                    return;
                }

                // Update order state based on partial flag
                order.OrderState = message.IsPartialReservation ? "PartialDelivered" : "StockReserved";
                
                if (message.IsPartialReservation)
                {
                    // Create lookup set for reserved SKUs
                    var reservedSkus = new HashSet<string>(message.Items.Select(i => i.Sku ?? string.Empty));
                    
                    // Mark items as Available or Unavailable based on reservation
                    foreach (var item in order.OrderItems)
                    {
                        item.Status = reservedSkus.Contains(item.Sku ?? string.Empty) ? "Available" : "Unavailable";
                    }
                    
                    var availableSkusLog = string.Join(", ", message.Items.Select(i => $"{i.Sku} (Qty: {i.Quantity})"));
                    _logger.LogWarning("⚠️ [PARTIAL FULFILLMENT] Order {OrderId} has partial stock availability. " +
                        "Available items: [{AvailableItems}] (CorrelationId: {CorrelationId})",
                        message.OrderId, availableSkusLog, message.CorrelationId);
                }
                else
                {
                    // Full stock reserved - mark all items as Available
                    foreach (var item in order.OrderItems)
                    {
                        item.Status = "Available";
                    }
                }
                
                _context.Orders.Update(order);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated Order {OrderId} state to {OrderState}, {ReservedCount}/{TotalCount} items marked as Available (CorrelationId: {CorrelationId})",
                    message.OrderId, order.OrderState, message.TotalReserved, message.TotalRequested, message.CorrelationId);
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
