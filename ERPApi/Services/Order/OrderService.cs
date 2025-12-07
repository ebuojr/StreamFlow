using Contracts.Dtos;
using Contracts.Events;
using ERPApi.DBContext;
using ERPApi.Repository.Order;
using MassTransit;

namespace ERPApi.Services.Order
{
    public class OrderService : IOrderService
    {
        private readonly IOrderRepository orderRepository;
        private readonly OrderDbContext context;
        private readonly IPublishEndpoint publishEndpoint;
        private readonly ILogger<OrderService> logger;

        public OrderService(
            IOrderRepository orderRepository,
            OrderDbContext context,
            IPublishEndpoint publishEndpoint,
            ILogger<OrderService> logger)
        {
            this.orderRepository = orderRepository;
            this.context = context;
            this.publishEndpoint = publishEndpoint;
            this.logger = logger;
        }

        public async Task<int> CreateAndSendOrderAsync(Entities.Model.Order order)
        {
            var correlationId = Guid.NewGuid().ToString();

            // Validate order
            var validationErrors = ValidateOrder(order);
            if (validationErrors.Any())
            {
                logger.LogWarning("Order validation failed. Errors: {Errors}", string.Join(", ", validationErrors));
                throw new ArgumentException($"Order validation failed: {string.Join(", ", validationErrors)}");
            }

            // Begin transaction - store order + MassTransit outbox message atomically
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                // 1. Save order in database
                var createdOrderNo = await orderRepository.CreateOrderAsync(order);
                logger.LogInformation("Order {OrderNo} created with ID {OrderId} [CorrelationId: {CorrelationId}]",
                    createdOrderNo, order.Id, correlationId);


                // 2. Build enriched OrderCreated event (Content Enricher pattern)
                var enrichedEvent = new OrderCreated
                {
                    #region Enrichment Details
                    OrderId = order.Id,
                    OrderNo = createdOrderNo,
                    OrderType = order.FindOrderType(),
                    Priority = order.GetPriority(),

                    // Enrich: Items for inventory check
                    Items = order.OrderItems.Select(i => new OrderItemDto
                    {
                        Sku = i.Sku ?? string.Empty,
                        Quantity = i.Quantity,
                        ProductName = i.Name ?? string.Empty,
                        UnitPrice = i.UnitPrice
                    }).ToList(),

                    // Enrich: Customer context
                    Customer = new CustomerDto
                    {
                        CustomerId = order.Customer.Id,
                        Name = $"{order.Customer.FirstName} {order.Customer.LastName}",
                        Email = order.Customer.Email,
                        CustomerType = "Regular" // Can be extended
                    },

                    // Enrich: Shipping context
                    ShippingAddress = new ShippingAddressDto
                    {
                        Street = order.ShippingAddress.Street,
                        City = order.ShippingAddress.City,
                        PostalCode = order.ShippingAddress.PostalCode,
                        State = order.ShippingAddress.State,
                        Country = order.ShippingAddress.Country
                    },

                    // Enrich: Order summary
                    TotalAmount = order.TotalAmount,
                    TotalItems = order.OrderItems.Count,
                    #endregion

                    CorrelationId = correlationId,
                    CreatedAt = DateTime.UtcNow
                };

                await publishEndpoint.Publish(enrichedEvent);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                logger.LogInformation(
                    "[ERP-Api] Order created and event published. OrderNo={OrderNo}, Type={OrderType}, Priority={Priority}",
                    createdOrderNo, enrichedEvent.OrderType, enrichedEvent.Priority);

                return createdOrderNo;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                logger.LogError(ex, "[ERP-Api] Failed to create order. OrderId={OrderId}",
                    order.Id);
                throw;
            }
        }

        private List<string> ValidateOrder(Entities.Model.Order order)
        {
            var errors = new List<string>();

            if (order == null)
                errors.Add("Order cannot be null");
            else
            {
                if (order.OrderItems == null || !order.OrderItems.Any())
                    errors.Add("Order must contain at least one item");

                if (order.Customer == null)
                    errors.Add("Customer information is required");

                if (order.ShippingAddress == null)
                    errors.Add("Shipping address is required");

                if (order.Payment == null)
                    errors.Add("Payment information is required");

                if (order.TotalAmount <= 0)
                    errors.Add("Total amount must be greater than zero");
            }

            return errors;
        }

        public async Task<IEnumerable<Entities.Model.Order>> GetAllOrders()
        {
            return await orderRepository.GetAllOrders();
        }

        public async Task<Entities.Model.Order> GetOrderById(Guid id)
        {
            return await orderRepository.GetOrderById(id);
        }

        public Task<IEnumerable<Entities.Model.Order>> GetOrderByState(string state)
        {
            return orderRepository.GetOrderByState(state);
        }

        public async Task<bool> UpdateOrderState(Guid id, string state)
        {
            return await orderRepository.UpdateOrderState(id, state);
        }

        public async Task<IEnumerable<MassTransit.EntityFrameworkCoreIntegration.OutboxMessage>> GetFaultedMessagesAsync()
        {
            return await orderRepository.GetFaultedMessagesAsync();
        }

    }
}
