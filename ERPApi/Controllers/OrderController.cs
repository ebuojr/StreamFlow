using Entities.Model;
using ERPApi.Services.Order;
using Microsoft.AspNetCore.Mvc;
using Contracts;

namespace ERPApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrderController : ControllerBase
    {
        private IOrderService _orderService;
        public OrderController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateOrderAsync([FromBody] Order order)
        {
            if (order == null)
                return BadRequest("Order cannot be null");

            try
            {
                var createdOrderNo = await _orderService.CreateOrderAsync(order);
                var response = new CreateOrderResponse
                {
                    OrderNo = createdOrderNo,
                    IsSuccessfullyCreated = true,
                    ErrorMessage = string.Empty
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                var response = new CreateOrderResponse
                {
                    OrderNo = 0,
                    IsSuccessfullyCreated = false,
                    ErrorMessage = ex.Message
                };
                return StatusCode(500, response);
            }
        }

        private static Order MapToEntity(Order dto)
        {
            return new Order
            {
                Id = dto.Id,
                OrderNo = dto.OrderNo,
                CreatedAt = dto.CreatedAt,
                OrderStatus = dto.OrderStatus,
                CountryCode = dto.CountryCode,
                IsPreOrder = dto.IsPreOrder,
                TotalAmount = dto.TotalAmount,
                OrderItems = dto.OrderItems?.Select(oi => new OrderItem
                {
                    Id = oi.Id,
                    OrderId = oi.OrderId,
                    Sku = oi.Sku,
                    Name = oi.Name,
                    Quantity = oi.Quantity,
                    UnitPrice = oi.UnitPrice,
                    TotalPrice = oi.TotalPrice
                }).ToList() ?? new List<OrderItem>(),
                Customer = dto.Customer == null ? null! : new Customer
                {
                    Id = dto.Customer.Id,
                    FirstName = dto.Customer.FirstName,
                    LastName = dto.Customer.LastName,
                    Email = dto.Customer.Email,
                    Phone = dto.Customer.Phone
                },
                Payment = dto.Payment == null ? null! : new Payment
                {
                    PaymentMethod = dto.Payment.PaymentMethod,
                    PaymentStatus = dto.Payment.PaymentStatus,
                    PaidAt = dto.Payment.PaidAt,
                    Currency = dto.Payment.Currency,
                    Amount = dto.Payment.Amount
                },
                ShippingAddress = dto.ShippingAddress == null ? null! : new Address
                {
                    Street = dto.ShippingAddress.Street,
                    City = dto.ShippingAddress.City,
                    State = dto.ShippingAddress.State,
                    PostalCode = dto.ShippingAddress.PostalCode,
                    Country = dto.ShippingAddress.Country
                }
            };
        }
    }
}
