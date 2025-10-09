using Entities.Model;
using ERPApi.Services.Order;
using Microsoft.AspNetCore.Mvc;

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
            var createdOrder = await _orderService.CreateOrderAsync(order);
            return createdOrder != null ? Ok(createdOrder) : BadRequest("Order could not be created");
        }
    }
}
