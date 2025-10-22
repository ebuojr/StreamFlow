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
            
            var logPrefix = message.IsPartialReservation ? "⚠️ [PARTIAL PICKING STARTED]" : "🔍 [PICKING STARTED]";
            _logger.LogInformation("{Prefix} Order {OrderId} | Type: {OrderType} | Items: {ItemCount} | Partial: {IsPartial} ({Reserved}/{Requested}) | CorrelationId: {CorrelationId}",
                logPrefix, message.OrderId, message.OrderType, message.Items.Count, message.IsPartialReservation, 
                message.TotalReserved, message.TotalRequested, message.CorrelationId);

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

                var completionPrefix = message.IsPartialReservation ? "✅ [PARTIAL PICKING COMPLETED]" : "✅ [PICKING COMPLETED]";
                _logger.LogInformation("{Prefix} Order {OrderId} | Picked {Picked}/{Requested} items | Actual time: {ActualTime}ms | Priority: {Priority} | Published OrderPicked event (CorrelationId: {CorrelationId})",
                    completionPrefix, message.OrderId, message.TotalReserved, message.TotalRequested, pickingTime, priority, message.CorrelationId);
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
