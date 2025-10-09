using Microsoft.AspNetCore.Mvc;
using OrderApi.Services.Order;

namespace OrderApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrderController : ControllerBase
    {
        private readonly IOrderService _orderService;

        public OrderController(IOrderService orderService)
        {
            _orderService = orderService ?? throw new ArgumentNullException(nameof(orderService));
        }

        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] Entities.Model.Order order)
        {
            if (order == null)
                return BadRequest("Order cannot be null.");

            var result = await _orderService.CreateOrderAsync(order);
            return Ok(new { OrderNo = result });
        }
    }
}
