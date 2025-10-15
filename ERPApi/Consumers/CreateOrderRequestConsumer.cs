using Contracts;
using ERPApi.Services.Order;
using MassTransit;

namespace ERPApi.Consumers
{
    /// <summary>
    /// Consumes CreateOrderRequest messages and creates orders using Request/Reply pattern.
    /// Responds with CreateOrderResponse containing order number or error details.
    /// MassTransit handles retries and DLC automatically.
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create order for Customer {CustomerId} (CorrelationId: {CorrelationId})",
                    request.Order.CustomerId, request.CorrelationId);

                // Respond with failure - let MassTransit retry policy handle DLC
                await context.RespondAsync(new CreateOrderResponse
                {
                    OrderNo = 0,
                    IsSuccessfullyCreated = false,
                    ErrorMessage = ex.Message + (ex.InnerException != null ? " | " + ex.InnerException.Message : string.Empty)
                });

                // Rethrow to trigger MassTransit retry policy and eventual DLC
                throw;
            }
        }
    }
}
