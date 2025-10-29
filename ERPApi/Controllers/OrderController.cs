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

        [HttpGet("{state}")]
        public async Task<IActionResult> GetOrdersByState(string state)
        {
            try
            {
                var orders = await _orderService.GetOrderByState(state);
                return Ok(orders);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("{id}/tracking")]
        public async Task<IActionResult> GetOrderTracking(Guid id)
        {
            try
            {
                var order = await _orderService.GetOrderById(id);
                if (order == null)
                    return NotFound($"Order with ID {id} not found.");

                var trackingResponse = new OrderTrackingResponse
                {
                    OrderId = order.Id,
                    OrderNo = order.OrderNo.ToString(),
                    CurrentState = order.OrderState,
                    StatusMessage = GetStatusMessage(order.OrderState),
                    CorrelationId = null, // CorrelationId not stored in Order entity
                    CreatedAt = order.CreatedAt,
                    LastUpdatedAt = null, // UpdatedAt not stored in Order entity
                    OrderType = order.FindOrderType(),
                    TotalAmount = order.TotalAmount,
                    StatusHistory = BuildStatusHistory(order)
                };

                return Ok(trackingResponse);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        private string GetStatusMessage(string state)
        {
            return state switch
            {
                "Created" => "Order received and is being processed",
                "StockReserved" => "Stock has been reserved for your order",
                "PartialDelivered" => "Some items are unavailable - partial fulfillment in progress",
                "Picked" => "Your order has been picked from the warehouse",
                "Packed" => "Your order has been packed and is ready for shipping",
                "StockUnavailable" => "All items in your order are out of stock",
                "Failed" => "There was an issue processing your order",
                "Pending" => "Order is pending",
                _ => "Your order is being processed"
            };
        }

        private List<OrderStatusHistoryItem> BuildStatusHistory(Order order)
        {
            var history = new List<OrderStatusHistoryItem>();

            // Always add Created state
            history.Add(new OrderStatusHistoryItem
            {
                State = "Created",
                StatusMessage = GetStatusMessage("Created"),
                Timestamp = order.CreatedAt
            });

            // Add intermediate states based on current state
            if (order.OrderState == "StockReserved" || order.OrderState == "Picked" || order.OrderState == "Packed")
            {
                history.Add(new OrderStatusHistoryItem
                {
                    State = "StockReserved",
                    StatusMessage = GetStatusMessage("StockReserved"),
                    Timestamp = order.CreatedAt.AddSeconds(1) // Approximate timing
                });
            }

            if (order.OrderState == "PartialDelivered" || order.OrderState == "Picked" || order.OrderState == "Packed")
            {
                if (order.OrderState == "PartialDelivered")
                {
                    history.Add(new OrderStatusHistoryItem
                    {
                        State = "PartialDelivered",
                        StatusMessage = GetStatusMessage("PartialDelivered"),
                        Timestamp = order.CreatedAt.AddSeconds(1) // Approximate timing
                    });
                }
            }

            if (order.OrderState == "Picked" || order.OrderState == "Packed")
            {
                history.Add(new OrderStatusHistoryItem
                {
                    State = "Picked",
                    StatusMessage = GetStatusMessage("Picked"),
                    Timestamp = order.CreatedAt.AddSeconds(5) // Approximate timing
                });
            }

            if (order.OrderState == "Packed")
            {
                history.Add(new OrderStatusHistoryItem
                {
                    State = "Packed",
                    StatusMessage = GetStatusMessage("Packed"),
                    Timestamp = order.CreatedAt.AddSeconds(8) // Approximate timing
                });
            }

            // Add failure states if applicable
            if (order.OrderState == "StockUnavailable")
            {
                history.Add(new OrderStatusHistoryItem
                {
                    State = "StockUnavailable",
                    StatusMessage = GetStatusMessage("StockUnavailable"),
                    Timestamp = order.CreatedAt.AddSeconds(1)
                });
            }

            if (order.OrderState == "Failed")
            {
                history.Add(new OrderStatusHistoryItem
                {
                    State = "Failed",
                    StatusMessage = GetStatusMessage("Failed"),
                    Timestamp = order.CreatedAt.AddSeconds(1)
                });
            }

            return history;
        }
    }
}
