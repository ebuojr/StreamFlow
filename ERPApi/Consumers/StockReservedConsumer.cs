using Contracts.Events;
using ERPApi.DBContext;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERPApi.Consumers
{
    public class StockReservedConsumer : IConsumer<StockReserved>
    {
        private readonly OrderDbContext _context;
        private readonly ILogger<StockReservedConsumer> _logger;

        public StockReservedConsumer(
            OrderDbContext context, 
            ILogger<StockReservedConsumer> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<StockReserved> context)
        {
            var message = context.Message;
            
            _logger.LogInformation("[ERP-Api] StockReserved event received. OrderId={OrderId}, Partial={IsPartial} ({Reserved}/{Requested})",
                message.OrderId, message.IsPartialReservation, message.TotalReserved, message.TotalRequested);

            try
            {
                var order = await _context.Orders
                    .Include(o => o.OrderItems)
                    .FirstOrDefaultAsync(o => o.Id == message.OrderId);

                if (order == null)
                {
                    _logger.LogWarning("[ERP-Api] Order not found. OrderId={OrderId}",
                        message.OrderId);
                    return;
                }

                order.OrderState = message.IsPartialReservation ? "PartialDelivered" : "StockReserved";
                
                if (message.IsPartialReservation)
                {
                    var reservedSkus = new HashSet<string>(message.Items.Select(i => i.Sku ?? string.Empty));
                    
                    foreach (var item in order.OrderItems)
                    {
                        item.Status = reservedSkus.Contains(item.Sku ?? string.Empty) ? "Available" : "Unavailable";
                    }
                    
                    var availableSkusLog = string.Join(", ", message.Items.Select(i => $"{i.Sku} (Qty: {i.Quantity})"));
                    _logger.LogWarning("[ERP-Api] Partial stock availability. OrderId={OrderId}, AvailableItems=[{AvailableItems}]",
                        message.OrderId, availableSkusLog);
                }
                else
                {
                    foreach (var item in order.OrderItems)
                    {
                        item.Status = "Available";
                    }
                }
                
                _context.Orders.Update(order);
                await _context.SaveChangesAsync();

                _logger.LogInformation("[ERP-Api] Order state updated. OrderId={OrderId}, State={OrderState}, Reserved={Reserved}/{Requested}",
                    message.OrderId, order.OrderState, message.TotalReserved, message.TotalRequested);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ERP-Api] Error updating order state. OrderId={OrderId}",
                    message.OrderId);
                throw;
            }
        }
    }
}
