using Contracts.Events;
using MassTransit;

namespace PickingService.Consumers
{
    /// <summary>
    /// Consumes PartialStockReserved events when only some items are available.
    /// Picks only the available items and publishes OrderPicked event.
    /// </summary>
    public class PartialStockReservedConsumer : IConsumer<PartialStockReserved>
    {
        private readonly ILogger<PartialStockReservedConsumer> _logger;

        public PartialStockReservedConsumer(ILogger<PartialStockReservedConsumer> logger)
        {
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<PartialStockReserved> context)
        {
            var message = context.Message;
            
            _logger.LogWarning(
                "⚠️ [PARTIAL PICKING STARTED] Order {OrderNo} | Type: {OrderType} | Available Items: {AvailableCount} | Unavailable Items: {UnavailableCount} | CorrelationId: {CorrelationId}",
                message.OrderNo, message.OrderType, message.AvailableItems.Count, message.UnavailableItems.Count, message.CorrelationId);

            try
            {
                // Simulate picking work for available items only (2-5 seconds)
                var pickingTime = Random.Shared.Next(2000, 5001);
                _logger.LogInformation(
                    "⏳ Picking Order {OrderNo} (Partial) - estimated time: {PickingTime}ms (CorrelationId: {CorrelationId})",
                    message.OrderNo, pickingTime, message.CorrelationId);
                
                await Task.Delay(pickingTime, context.CancellationToken);

                // Picking complete for available items - publish OrderPicked event
                var orderPicked = new OrderPicked
                {
                    OrderId = message.OrderId,
                    CorrelationId = message.CorrelationId,
                    OrderType = message.OrderType,
                    PickedAt = DateTime.UtcNow,
                    Items = message.AvailableItems, // Only available items picked
                    Customer = message.Customer,
                    ShippingAddress = message.ShippingAddress
                };

                // Publish with priority header for downstream priority queue
                var priority = message.OrderType == "Priority" ? (byte)9 : (byte)1;
                await context.Publish(orderPicked, ctx =>
                {
                    ctx.Headers.Set("priority", priority);
                });

                _logger.LogInformation(
                    "✅ [PARTIAL PICKING COMPLETED] Order {OrderNo} | Picked {PickedCount}/{TotalCount} items | Actual time: {ActualTime}ms | Priority: {Priority} | Published OrderPicked event (CorrelationId: {CorrelationId})",
                    message.OrderNo, message.AvailableItems.Count, 
                    message.AvailableItems.Count + message.UnavailableItems.Count, 
                    pickingTime, priority, message.CorrelationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [PARTIAL PICKING FAILED] Order {OrderNo} (CorrelationId: {CorrelationId})",
                    message.OrderNo, message.CorrelationId);
                throw; // Let MassTransit handle retry/DLC
            }
        }
    }
}
