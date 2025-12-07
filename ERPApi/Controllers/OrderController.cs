using Entities.Model;
using ERPApi.DBContext;
using ERPApi.Services.Order;
using Microsoft.AspNetCore.Mvc;
using Contracts;
using MassTransit;

namespace ERPApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrderController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly OrderDbContext _context;
        private readonly ILogger<OrderController> _logger;

        public OrderController(
            IOrderService orderService,
            IPublishEndpoint publishEndpoint,
            OrderDbContext context,
            ILogger<OrderController> logger)
        {
            _orderService = orderService;
            _publishEndpoint = publishEndpoint;
            _context = context;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> CreateOrderAsync([FromBody] Order order)
        {
            if (order == null)
                return BadRequest(new CreateOrderResponse() { ErrorMessage = "Order can't be null" });

            try
            {
                // Create order and get enriched event (order is added to context but not saved yet)
                var orderCreatedEvent = await _orderService.CreateOrderAsync(order);

                // Publish event directly to RabbitMQ
                // Note: API-initiated publishes don't use the outbox (no consumer context).
                // For guaranteed delivery from API endpoints, consider using the Request/Response
                // pattern via OrderApi -> CreateOrderRequestConsumer instead.
                await _publishEndpoint.Publish(orderCreatedEvent);
                
                // Save changes to persist the order
                await _context.SaveChangesAsync();

                _logger.LogInformation("[ERP-Api] Order created via API. OrderNo={OrderNo}", orderCreatedEvent.OrderNo);

                var response = new CreateOrderResponse
                {
                    OrderNo = orderCreatedEvent.OrderNo,
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
                    ErrorMessage = ex.Message + ex.InnerException?.Message
                };

                return StatusCode(500, response);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAllOrders()
        {
            try
            {
                var orders = await _orderService.GetAllOrders();
                return Ok(orders);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetOrderById(Guid id)
        {
            try
            {
                var order = await _orderService.GetOrderById(id);
                if (order == null)
                    return NotFound($"Order with ID {id} not found.");

                return Ok(order);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}
