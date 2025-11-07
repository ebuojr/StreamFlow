using Contracts.Dtos;
using Contracts.Events;
using MassTransit;

namespace InventoryService.Consumers
{
    public class OrderCreatedConsumer : IConsumer<OrderCreated>
    {
        private const double ITEM_AVAILABILITY_RATE = 0.80;

        private readonly ILogger<OrderCreatedConsumer> _logger;
        private readonly Random _random = new Random();

        public OrderCreatedConsumer(ILogger<OrderCreatedConsumer> logger)
        {
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<OrderCreated> context)
        {
            var orderCreated = context.Message;
            
            _logger.LogInformation(
                "[Inventory-Service] Order received. OrderId={OrderId}, OrderNo={OrderNo}, Type={OrderType}, Priority={Priority}, Items={TotalItems}, CorrelationId={CorrelationId}",
                orderCreated.OrderId,
                orderCreated.OrderNo,
                orderCreated.OrderType,
                orderCreated.Priority,
                orderCreated.TotalItems,
                orderCreated.CorrelationId);

            await Task.Delay(500);

            var availableItems = new List<OrderItemDto>();
            var unavailableItems = new List<OrderItemDto>();

            foreach (var item in orderCreated.Items)
            {
                bool isAvailable = _random.NextDouble() < ITEM_AVAILABILITY_RATE;
                
                if (isAvailable)
                {
                    availableItems.Add(item);
                }
                else
                {
                    unavailableItems.Add(item);
                }
            }

            if (availableItems.Count == 0)
            {
                var unavailableSkus = string.Join(", ", unavailableItems.Select(i => i.Sku));
                
                _logger.LogWarning(
                    "[Inventory-Service] No items available. OrderNo={OrderNo}, UnavailableSkus=[{Skus}], CorrelationId={CorrelationId}",
                    orderCreated.OrderNo,
                    unavailableSkus,
                    orderCreated.CorrelationId);

                await context.Publish(new StockUnavailable
                {
                    OrderId = orderCreated.OrderId,
                    UnavailableSkus = unavailableItems.Select(i => i.Sku).ToList(),
                    Reason = $"All {unavailableItems.Count} items out of stock",
                    CorrelationId = orderCreated.CorrelationId,
                    CheckedAt = DateTime.UtcNow
                });
            }
            else
            {
                bool isPartial = unavailableItems.Count > 0;
                int totalRequested = orderCreated.Items.Count;
                int totalReserved = availableItems.Count;
                
                if (isPartial)
                {
                    var availableSkus = string.Join(", ", availableItems.Select(i => i.Sku));
                    var unavailableSkus = string.Join(", ", unavailableItems.Select(i => i.Sku));
                    
                    _logger.LogWarning(
                        "[Inventory-Service] Partial availability. OrderNo={OrderNo}, Available=[{AvailableSkus}], Unavailable=[{UnavailableSkus}], CorrelationId={CorrelationId}",
                        orderCreated.OrderNo,
                        availableSkus,
                        unavailableSkus,
                        orderCreated.CorrelationId);
                }
                else
                {
                    _logger.LogInformation(
                        "[Inventory-Service] All items available. OrderNo={OrderNo}, CorrelationId={CorrelationId}",
                        orderCreated.OrderNo,
                        orderCreated.CorrelationId);
                }

                await context.Publish(new StockReserved
                {
                    OrderId = orderCreated.OrderId,
                    OrderType = orderCreated.OrderType,
                    Items = availableItems,
                    Customer = orderCreated.Customer,
                    ShippingAddress = orderCreated.ShippingAddress,
                    CorrelationId = orderCreated.CorrelationId,
                    ReservedAt = DateTime.UtcNow,
                    IsPartialReservation = isPartial,
                    TotalRequested = totalRequested,
                    TotalReserved = totalReserved
                });
            }
        }
    }
}
