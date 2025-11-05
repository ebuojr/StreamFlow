using Contracts.Events;
using MassTransit;

namespace PackingService.Consumers
{
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
            
            _logger.LogInformation("[Packing-Service] Packing started. OrderId={OrderId}, Type={OrderType}, Items={ItemCount}, CorrelationId={CorrelationId}",
                message.OrderId, message.OrderType, message.Items.Count, message.CorrelationId);

            try
            {
                var packingTime = Random.Shared.Next(1500, 3001);
                _logger.LogInformation("[Packing-Service] Processing order. OrderId={OrderId}, EstimatedTime={PackingTime}ms, CorrelationId={CorrelationId}",
                    message.OrderId, packingTime, message.CorrelationId);
                
                await Task.Delay(packingTime, context.CancellationToken);

                var totalItems = message.Items.Sum(i => i.Quantity);
                var boxSize = totalItems switch
                {
                    <= 2 => "Small",
                    <= 5 => "Standard",
                    _ => "Large"
                };
                var totalWeight = totalItems * 0.5m;

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

                _logger.LogInformation("[Packing-Service] Packing completed. OrderId={OrderId}, Time={ActualTime}ms, Box={BoxSize}, Weight={Weight}kg, CorrelationId={CorrelationId}",
                    message.OrderId, packingTime, boxSize, totalWeight, message.CorrelationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Packing-Service] Packing failed. OrderId={OrderId}, CorrelationId={CorrelationId}",
                    message.OrderId, message.CorrelationId);
                throw;
            }
        }
    }
}
