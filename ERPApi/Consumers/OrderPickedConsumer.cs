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
                    .FirstOrDefaultAsync(o => o.Id == message.OrderId);

                if (order == null)
                {
                    _logger.LogWarning("Order {OrderId} not found when processing OrderPicked event (CorrelationId: {CorrelationId})",
                        message.OrderId, message.CorrelationId);
                    return;
                }

                order.OrderState = "Picked";
                _context.Orders.Update(order);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated Order {OrderId} state to Picked (CorrelationId: {CorrelationId})",
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
