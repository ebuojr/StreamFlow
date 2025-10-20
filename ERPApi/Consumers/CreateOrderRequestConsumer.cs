using Contracts;
using ERPApi.Services.Order;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace ERPApi.Consumers
{
    /// <summary>
    /// Consumes CreateOrderRequest messages and creates orders using Request/Reply pattern.
    /// Responds with CreateOrderResponse containing order number or error details.
    /// 
    /// Error Handling Strategy:
    /// - Transient errors (DB deadlocks, connection issues): Throws to trigger MassTransit retry (3 attempts)
    /// - Business errors (validation, not found): Responds with error, NO retry (prevents wasteful retries)
    /// 
    /// EIP Pattern: Request-Reply with selective retry strategy
    /// </summary>
    public class CreateOrderRequestConsumer : IConsumer<CreateOrderRequest>
    {
        private readonly IOrderService _orderService;
        private readonly ILogger<CreateOrderRequestConsumer> _logger;

        public CreateOrderRequestConsumer(IOrderService orderService, ILogger<CreateOrderRequestConsumer> logger)
        {
            _orderService = orderService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<CreateOrderRequest> context)
        {
            var request = context.Message;
            
            _logger.LogInformation("Received CreateOrderRequest for Customer {CustomerId} (CorrelationId: {CorrelationId})",
                request.Order.CustomerId, request.CorrelationId);

            try
            {
                // Create order and publish enriched event via Transactional Outbox
                var orderNo = await _orderService.CreateAndSendOrderAsync(request.Order);

                _logger.LogInformation("Successfully created Order {OrderNo} for Customer {CustomerId} (CorrelationId: {CorrelationId})",
                    orderNo, request.Order.CustomerId, request.CorrelationId);

                // Respond with success
                await context.RespondAsync(new CreateOrderResponse
                {
                    OrderNo = orderNo,
                    IsSuccessfullyCreated = true,
                    ErrorMessage = string.Empty
                });
            }
            catch (DbUpdateException ex) // Transient database errors (deadlocks, connection issues)
            {
                _logger.LogError(ex, "Transient DB error creating order for Customer {CustomerId} - will retry (CorrelationId: {CorrelationId})",
                    request.Order.CustomerId, request.CorrelationId);
                
                // Rethrow to trigger MassTransit retry policy for transient errors
                throw;
            }
            catch (Exception ex) // Business validation errors (non-retryable)
            {
                _logger.LogError(ex, "Business error creating order for Customer {CustomerId} - no retry (CorrelationId: {CorrelationId})",
                    request.Order.CustomerId, request.CorrelationId);

                // Respond with error - client needs immediate feedback
                await context.RespondAsync(new CreateOrderResponse
                {
                    OrderNo = 0,
                    IsSuccessfullyCreated = false,
                    ErrorMessage = ex.Message + (ex.InnerException != null ? " | " + ex.InnerException.Message : string.Empty)
                });

                // ✅ DON'T throw - request complete, client already notified
                // No point retrying business validation errors (customer not found, invalid SKU, etc.)
            }
        }
    }
}
