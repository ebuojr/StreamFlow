using Entities.Model;
using Contracts.Events;
using ERPApi.DBContext;
using ERPApi.Services.Order;
using ERPApi.Repository.Order;
using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace StreamFlow.Tests
{
    public class OrderServiceTests
    {
        [Fact]
        public void Order_FindOrderType_DanishOrderIsPriority()
        {
            // Arrange
            var order = new Order
            {
                ShippingAddress = new Address { Country = "DK" }
            };

            // Act
            var orderType = order.FindOrderType();

            // Assert
            orderType.Should().Be("Priority");
        }

        [Fact]
        public void Order_FindOrderType_NonDanishOrderIsStandard()
        {
            // Arrange - Swedish order
            var order = new Order
            {
                ShippingAddress = new Address { Country = "SE" }
            };

            // Act
            var orderType = order.FindOrderType();

            // Assert
            orderType.Should().Be("Standard");
        }

        [Fact]
        public void Order_GetPriority_DanishOrderHasPriority9()
        {
            // Arrange
            var order = new Order
            {
                ShippingAddress = new Address { Country = "DK" }
            };

            // Act
            var priority = order.GetPriority();

            // Assert
            priority.Should().Be(9);
        }

        [Fact]
        public void Order_GetPriority_StandardOrderHasPriority1()
        {
            // Arrange - German order
            var order = new Order
            {
                ShippingAddress = new Address { Country = "DE" }
            };

            // Act
            var priority = order.GetPriority();

            // Assert
            priority.Should().Be(1);
        }

        [Fact]
        public async Task OrderService_ValidateOrder_RejectsNullOrder()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var mockPublishEndpoint = new Mock<IPublishEndpoint>();
            var mockLogger = new Mock<ILogger<OrderService>>();
            var mockRepository = new Mock<IOrderRepository>();
            
            var service = new OrderService(
                mockRepository.Object,
                context,
                mockPublishEndpoint.Object,
                mockLogger.Object
            );

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                service.CreateAndSendOrderAsync(null!));
        }

        [Fact]
        public async Task OrderService_ValidateOrder_RejectsOrderWithNoItems()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var mockPublishEndpoint = new Mock<IPublishEndpoint>();
            var mockLogger = new Mock<ILogger<OrderService>>();
            var mockRepository = new Mock<IOrderRepository>();
            
            var service = new OrderService(
                mockRepository.Object,
                context,
                mockPublishEndpoint.Object,
                mockLogger.Object
            );

            var order = CreateValidOrder();
            order.OrderItems = new List<OrderItem>(); // No items

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() => 
                service.CreateAndSendOrderAsync(order));
            exception.Message.Should().Contain("at least one item");
        }

        [Fact]
        public async Task OrderService_ValidateOrder_RejectsOrderWithNoCustomer()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var mockPublishEndpoint = new Mock<IPublishEndpoint>();
            var mockLogger = new Mock<ILogger<OrderService>>();
            var mockRepository = new Mock<IOrderRepository>();
            
            var service = new OrderService(
                mockRepository.Object,
                context,
                mockPublishEndpoint.Object,
                mockLogger.Object
            );

            var order = CreateValidOrder();
            order.Customer = null!; // No customer

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() => 
                service.CreateAndSendOrderAsync(order));
            exception.Message.Should().Contain("Customer");
        }

        [Fact]
        public async Task OrderDbContext_HasMassTransitOutboxTables()
        {
            // Arrange
            var context = CreateInMemoryDbContext();

            // Act - Check if MassTransit outbox tables exist in model
            var outboxMessageType = context.Model.GetEntityTypes()
                .FirstOrDefault(t => t.ClrType.Name.Contains("OutboxMessage"));
            
            var outboxStateType = context.Model.GetEntityTypes()
                .FirstOrDefault(t => t.ClrType.Name.Contains("OutboxState"));

            // Assert - Verify outbox pattern is configured
            outboxMessageType.Should().NotBeNull("MassTransit OutboxMessage should be configured");
            outboxStateType.Should().NotBeNull("MassTransit OutboxState should be configured");
        }

        [Fact]
        public async Task OrderDbContext_OrderHasRowVersionProperty()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var entityType = context.Model.FindEntityType(typeof(Order));

            // Act - Check if RowVersion property is configured as concurrency token
            var rowVersionProperty = entityType?.FindProperty("RowVersion");

            // Assert - Verify RowVersion is configured for optimistic concurrency
            rowVersionProperty.Should().NotBeNull("RowVersion property should exist");
            rowVersionProperty!.IsConcurrencyToken.Should().BeTrue("RowVersion should be marked as concurrency token");
        }

        [Fact]
        public async Task Order_IsStoredWithOwnedEntities()
        {
            // Arrange
            var context = CreateInMemoryDbContext();
            var order = CreateValidOrder();

            // Act
            context.Orders.Add(order);
            await context.SaveChangesAsync();

            // Assert - Verify owned entities are saved
            var savedOrder = await context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == order.Id);

            savedOrder.Should().NotBeNull();
            savedOrder!.Customer.Should().NotBeNull();
            savedOrder.ShippingAddress.Should().NotBeNull();
            savedOrder.Payment.Should().NotBeNull();
            savedOrder.Customer.FirstName.Should().Be("Mette");
            savedOrder.ShippingAddress.Country.Should().Be("DK");
        }

        private OrderDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<OrderDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            return new OrderDbContext(options);
        }

        private Order CreateValidOrder()
        {
            return new Order
            {
                Id = Guid.NewGuid(),
                TotalAmount = 599.00m,
                CountryCode = "DK",
                Customer = new Customer
                {
                    Id = Guid.NewGuid(),
                    FirstName = "Mette",
                    LastName = "Hansen",
                    Email = "mette.hansen@example.dk",
                    Phone = "+45-55-66-77-88"
                },
                ShippingAddress = new Address
                {
                    Street = "Vestergade 123",
                    City = "København",
                    State = "",
                    PostalCode = "2100",
                    Country = "DK"
                },
                OrderItems = new List<OrderItem>
                {
                    new OrderItem
                    {
                        Id = Guid.NewGuid(),
                        Sku = "Fashion-001",
                        Name = "Plus Size Maxi Dress",
                        Quantity = 1,
                        UnitPrice = 599.00m,
                        TotalPrice = 599.00m
                    }
                },
                Payment = new Payment
                {
                    PaymentMethod = "Card",
                    PaymentStatus = "Pending",
                    Amount = 599.00m,
                    Currency = "DKK"
                }
            };
        }
    }
}
