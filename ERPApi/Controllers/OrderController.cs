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
    }
}
