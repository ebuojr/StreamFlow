using Contracts.Events;
using MassTransit;

namespace InventoryService.Consumers
{
    public class OrderCreatedConsumer : IConsumer<OrderCreated>
    {
        private readonly ILogger<OrderCreatedConsumer> _logger;

        public OrderCreatedConsumer(ILogger<OrderCreatedConsumer> logger)
        {
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<OrderCreated> context)
        {
            var orderCreated = context.Message;
            
            _logger.LogInformation(
                "💚 [INVENTORY] Received OrderCreated: OrderId={OrderId}, OrderNo={OrderNo}, Type={OrderType}, Priority={Priority}, TotalItems={TotalItems}, CorrelationId={CorrelationId}",
                orderCreated.OrderId,
                orderCreated.OrderNo,
                orderCreated.OrderType,
                orderCreated.Priority,
                orderCreated.TotalItems,
                orderCreated.CorrelationId);

            // Content-Based Router: Route based on order type and priority
            if (orderCreated.OrderType == "Priority" && orderCreated.Priority == 9) // DK High Priority
            {
                _logger.LogInformation(
                    "💚 [INVENTORY] High-priority DK order detected. Fast-tracking to picking. [OrderNo={OrderNo}, CorrelationId={CorrelationId}]",
                    orderCreated.OrderNo,
                    orderCreated.CorrelationId);

                // Simulate inventory check (always in stock for demo)
                await Task.Delay(100); // Simulate quick check

                // Publish StockReserved to picking-queue
                await context.Publish(new StockReserved
                {
                    OrderId = orderCreated.OrderId,
                    OrderType = orderCreated.OrderType,
                    Items = orderCreated.Items,
                    Customer = orderCreated.Customer,
                    ShippingAddress = orderCreated.ShippingAddress,
                    CorrelationId = orderCreated.CorrelationId,
                    ReservedAt = DateTime.UtcNow
                });

                _logger.LogInformation(
                    "💚 [INVENTORY] StockReserved published for OrderNo={OrderNo} [CorrelationId={CorrelationId}]",
                    orderCreated.OrderNo,
                    orderCreated.CorrelationId);
            }
            else
            {
                _logger.LogInformation(
                    "💚 [INVENTORY] Standard order. Performing normal inventory check. [OrderNo={OrderNo}, CorrelationId={CorrelationId}]",
                    orderCreated.OrderNo,
                    orderCreated.CorrelationId);

                // Simulate inventory check
                await Task.Delay(500); // Simulate normal check

                // Publish StockReserved
                await context.Publish(new StockReserved
                {
                    OrderId = orderCreated.OrderId,
                    OrderType = orderCreated.OrderType,
                    Items = orderCreated.Items,
                    Customer = orderCreated.Customer,
                    ShippingAddress = orderCreated.ShippingAddress,
                    CorrelationId = orderCreated.CorrelationId,
                    ReservedAt = DateTime.UtcNow
                });

                _logger.LogInformation(
                    "💚 [INVENTORY] StockReserved published for OrderNo={OrderNo} [CorrelationId={CorrelationId}]",
                    orderCreated.OrderNo,
                    orderCreated.CorrelationId);
            }

            await Task.CompletedTask;
        }
    }
}
