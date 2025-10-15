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
            
            _logger.LogInformation("Received StockReserved event for Order {OrderId} (CorrelationId: {CorrelationId})",
                message.OrderId, message.CorrelationId);

            try
            {
                var order = await _context.Orders
                    .FirstOrDefaultAsync(o => o.Id == message.OrderId);

                if (order == null)
                {
                    _logger.LogWarning("Order {OrderId} not found when processing StockReserved event (CorrelationId: {CorrelationId})",
                        message.OrderId, message.CorrelationId);
                    return;
                }

                order.OrderState = "StockReserved";
                _context.Orders.Update(order);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated Order {OrderId} state to StockReserved (CorrelationId: {CorrelationId})",
                    message.OrderId, message.CorrelationId);
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
