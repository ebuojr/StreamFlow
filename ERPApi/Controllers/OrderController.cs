using Entities.Model;
using ERPApi.Services.Order;
using Microsoft.AspNetCore.Mvc;
using Contracts;
using Contracts.DTOs;

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
                return BadRequest(new CreateOrderResponse() { ErrorMessage = "Order can't be null" });

            try
            {
                var createdOrderNo = await _orderService.CreateAndSendOrderAsync(order);
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

        [HttpGet("dead-letter")]
        public async Task<IActionResult> GetDeadLetterMessages()
        {
            try
            {
                var faultedMessages = await _orderService.GetFaultedMessagesAsync();
                return Ok(faultedMessages);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}
