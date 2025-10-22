using Contracts.Dtos;
using Contracts.Events;
using MassTransit;

namespace InventoryService.Consumers
{
    public class OrderCreatedConsumer : IConsumer<OrderCreated>
    {
        private readonly ILogger<OrderCreatedConsumer> _logger;
        private readonly Random _random = new Random();
        private const double ITEM_AVAILABILITY_RATE = 0.80; // 80% chance each item is in stock

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

            // Perform random inventory check (80% availability per item)
            var checkDelay = (orderCreated.OrderType == "Priority" && orderCreated.Priority == 9) ? 100 : 500;
            await Task.Delay(checkDelay); // Simulate inventory check time

            // Check availability for each item (random: 80% chance each is in stock)
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

            // Scenario 1: NO items available (rare)
            if (availableItems.Count == 0)
            {
                var unavailableSkus = string.Join(", ", unavailableItems.Select(i => i.Sku));
                
                _logger.LogWarning(
                    "❌ [INVENTORY] NO items available for OrderNo={OrderNo}. Unavailable SKUs: [{Skus}]. Publishing StockUnavailable. [CorrelationId={CorrelationId}]",
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
            // Scenario 2: FULL or PARTIAL availability (merged into single event)
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
                        "⚠️ [INVENTORY] PARTIAL availability for OrderNo={OrderNo}. Available: [{AvailableSkus}], Unavailable: [{UnavailableSkus}]. Publishing StockReserved with IsPartialReservation=true. [CorrelationId={CorrelationId}]",
                        orderCreated.OrderNo,
                        availableSkus,
                        unavailableSkus,
                        orderCreated.CorrelationId);
                }
                else
                {
                    _logger.LogInformation(
                        "💚 [INVENTORY] ✅ All items available for OrderNo={OrderNo}. Publishing StockReserved with IsPartialReservation=false. [CorrelationId={CorrelationId}]",
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
