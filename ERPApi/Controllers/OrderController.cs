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

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateOrderStatus(Guid id, [FromBody] string status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return BadRequest("Status cannot be null or empty.");

            try
            {
                var isUpdated = await _orderService.UpdateOrderStatus(id, status);
                if (!isUpdated)
                    return NotFound($"Order with ID {id} not found.");

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}
