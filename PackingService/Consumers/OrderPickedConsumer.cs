using Contracts.Events;
using MassTransit;

namespace PackingService.Consumers
{
    /// <summary>
    /// Consumes OrderPicked events from PickingService and simulates packing work.
    /// Uses enriched event data (Content Enricher pattern) - no HTTP calls needed.
    /// Publishes OrderPacked event when packing is complete (final state).
    /// </summary>
    public class OrderPickedConsumer : IConsumer<OrderPicked>
    {
        private readonly ILogger<OrderPickedConsumer> _logger;

        public OrderPickedConsumer(ILogger<OrderPickedConsumer> logger)
        {
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<OrderPicked> context)
        {
            var message = context.Message;
            
            _logger.LogInformation("📦 [PACKING STARTED] Order {OrderId} | Type: {OrderType} | Items: {ItemCount} | CorrelationId: {CorrelationId}",
                message.OrderId, message.OrderType, message.Items.Count, message.CorrelationId);

            try
            {
                // Simulate packing work (1.5-3 seconds)
                var packingTime = Random.Shared.Next(1500, 3001);
                _logger.LogInformation("⏳ Packing Order {OrderId} - estimated time: {PackingTime}ms (CorrelationId: {CorrelationId})",
                    message.OrderId, packingTime, message.CorrelationId);
                
                await Task.Delay(packingTime, context.CancellationToken);

                // Calculate box size and weight based on items
                var totalItems = message.Items.Sum(i => i.Quantity);
                var boxSize = totalItems switch
                {
                    <= 2 => "Small",
                    <= 5 => "Standard",
                    _ => "Large"
                };
                var totalWeight = totalItems * 0.5m; // Assume 0.5kg per item

                // Packing complete - publish OrderPacked event (final event)
                var orderPacked = new OrderPacked
                {
                    OrderId = message.OrderId,
                    CorrelationId = message.CorrelationId,
                    PackedAt = DateTime.UtcNow,
                    PackedBy = "System",
                    TotalWeight = totalWeight,
                    BoxSize = boxSize,
                    Items = message.Items,
                    ShippingAddress = message.ShippingAddress
                };

                await context.Publish(orderPacked);

                _logger.LogInformation("✅ [PACKING COMPLETED] Order {OrderId} | Actual time: {ActualTime}ms | Box: {BoxSize} | Weight: {Weight}kg | Published OrderPacked event (CorrelationId: {CorrelationId})",
                    message.OrderId, packingTime, boxSize, totalWeight, message.CorrelationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [PACKING FAILED] Order {OrderId} (CorrelationId: {CorrelationId})",
                    message.OrderId, message.CorrelationId);
                throw; // Let MassTransit handle retry/DLC
            }
        }
    }
}
