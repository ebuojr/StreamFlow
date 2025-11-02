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

        public OrderPackedConsumer(
            OrderDbContext context, 
            ILogger<OrderPackedConsumer> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<OrderPacked> context)
        {
            var message = context.Message;
            
            _logger.LogInformation("Received OrderPacked event for Order {OrderId} (CorrelationId: {CorrelationId})",
                message.OrderId, message.CorrelationId);

            var maxRetries = 3;
            var retryCount = 0;
            
            while (retryCount < maxRetries)
            {
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

                    order.OrderState = "Packed";
                    
                    var packedSkus = new HashSet<string>(message.Items.Select(i => i.Sku ?? string.Empty));
                    
                    foreach (var item in order.OrderItems)
                    {
                        if (packedSkus.Contains(item.Sku ?? string.Empty))
                        {
                            item.Status = "Packed";
                        }
                    }
                    
                    _context.Orders.Update(order);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Updated Order {OrderId} state to Packed (final state), {PackedCount} items marked as Packed (CorrelationId: {CorrelationId})",
                        message.OrderId, message.Items.Count, message.CorrelationId);
                    
                    return;
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    retryCount++;
                    _logger.LogWarning(ex, "Concurrency conflict updating order. OrderId={OrderId}, Retry={RetryCount}/{MaxRetries}",
                        message.OrderId, retryCount, maxRetries);
                    
                    var entry = ex.Entries.FirstOrDefault();
                    if (entry != null)
                    {
                        await entry.ReloadAsync();
                    }
                    
                    if (retryCount >= maxRetries)
                    {
                        _logger.LogError(ex, "Failed to update order after {MaxRetries} attempts. OrderId={OrderId}",
                            maxRetries, message.OrderId);
                        throw;
                    }
                    
                    await Task.Delay(TimeSpan.FromMilliseconds(100 * retryCount));
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
}
