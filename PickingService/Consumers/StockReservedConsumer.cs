using Contracts.Events;
using MassTransit;

namespace PickingService.Consumers
{
    /// <summary>
    /// Consumes StockReserved events from InventoryApi and simulates picking work.
    /// Uses enriched event data (Content Enricher pattern) - no HTTP calls needed.
    /// Publishes OrderPicked event when picking is complete.
    /// Supports priority queue with 9=Priority (DK), 1=Standard.
    /// </summary>
    public class StockReservedConsumer : IConsumer<StockReserved>
    {
        private readonly ILogger<StockReservedConsumer> _logger;

        public StockReservedConsumer(ILogger<StockReservedConsumer> logger)
        {
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<StockReserved> context)
        {
            var message = context.Message;
            
            _logger.LogInformation("🔍 [PICKING STARTED] Order {OrderId} | Type: {OrderType} | Items: {ItemCount} | CorrelationId: {CorrelationId}",
                message.OrderId, message.OrderType, message.Items.Count, message.CorrelationId);

            try
            {
                // Simulate picking work (2-5 seconds)
                var pickingTime = Random.Shared.Next(2000, 5001);
                _logger.LogInformation("⏳ Picking Order {OrderId} - estimated time: {PickingTime}ms (CorrelationId: {CorrelationId})",
                    message.OrderId, pickingTime, message.CorrelationId);
                
                await Task.Delay(pickingTime, context.CancellationToken);

                // Picking complete - publish OrderPicked event with enriched data
                var orderPicked = new OrderPicked
                {
                    OrderId = message.OrderId,
                    CorrelationId = message.CorrelationId,
                    OrderType = message.OrderType,
                    PickedAt = DateTime.UtcNow,
                    Items = message.Items,
                    Customer = message.Customer,
                    ShippingAddress = message.ShippingAddress
                };

                // Publish with priority header for downstream priority queue
                var priority = message.OrderType == "Priority" ? (byte)9 : (byte)1;
                await context.Publish(orderPicked, ctx =>
                {
                    ctx.Headers.Set("priority", priority);
                });

                _logger.LogInformation("✅ [PICKING COMPLETED] Order {OrderId} | Actual time: {ActualTime}ms | Priority: {Priority} | Published OrderPicked event (CorrelationId: {CorrelationId})",
                    message.OrderId, pickingTime, priority, message.CorrelationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [PICKING FAILED] Order {OrderId} (CorrelationId: {CorrelationId})",
                    message.OrderId, message.CorrelationId);
                throw; // Let MassTransit handle retry/DLC
            }
        }
    }
}
