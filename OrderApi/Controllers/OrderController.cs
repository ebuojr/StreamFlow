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

        [HttpGet]
        public async Task<IActionResult> GetOrders()
        {
            var orders = await _orderService.GetOrdersAsync();
            return Ok(orders);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetOrderById(Guid id)
        {
            var order = await _orderService.GetOrderByIdAsync(id);

            if (order == null)
                return NotFound();

            return Ok(order);
        }

        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] Model.Order order)
        {
            if (order == null)
                return BadRequest("Order cannot be null.");

            var result = await _orderService.CreateOrderAsync(order);
            if (!result)
                return StatusCode(500, "A problem happened while handling your request.");
            else
                return Ok();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteOrder(Guid id)
        {
            var order = await _orderService.GetOrderByIdAsync(id);
            if (order == null)
                return NotFound();

            var result = await _orderService.RemoveOrderAsync(order);
            if (!result)
                return StatusCode(500, "A problem happened while handling your request.");

            return Ok();
        }
    }
}
