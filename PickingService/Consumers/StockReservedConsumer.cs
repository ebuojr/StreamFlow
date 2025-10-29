using Contracts.Events;
using MassTransit;

namespace PickingService.Consumers
{
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
            
            var partial = message.IsPartialReservation ? "Partial" : "Full";
            _logger.LogInformation("[Picking-Service] Picking started. OrderId={OrderId}, Type={OrderType}, Items={ItemCount}, Status={Status} ({Reserved}/{Requested})",
                message.OrderId, message.OrderType, message.Items.Count, partial, message.TotalReserved, message.TotalRequested);

            try
            {
                var pickingTime = Random.Shared.Next(2000, 5001);
                _logger.LogInformation("[Picking-Service] Processing order. OrderId={OrderId}, EstimatedTime={PickingTime}ms",
                    message.OrderId, pickingTime);
                
                await Task.Delay(pickingTime, context.CancellationToken);

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

                var priority = message.OrderType == "Priority" ? (byte)9 : (byte)1;
                await context.Publish(orderPicked, ctx =>
                {
                    ctx.Headers.Set("priority", priority);
                });

                _logger.LogInformation("[Picking-Service] Picking completed. OrderId={OrderId}, Picked={Picked}/{Requested}, Time={ActualTime}ms, Priority={Priority}",
                    message.OrderId, message.TotalReserved, message.TotalRequested, pickingTime, priority);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Picking-Service] Picking failed. OrderId={OrderId}",
                    message.OrderId);
                throw;
            }
        }
    }
}
