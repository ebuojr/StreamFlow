using Contracts;
using Contracts.Events;
using ERPApi.Services.Order;
using ERPApi.Services.Validation;
using FluentValidation;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ERPApi.Consumers
{
    /// <summary>
    /// Consumes CreateOrderRequest messages and creates orders using Request/Reply pattern.
    /// Responds with CreateOrderResponse containing order number or error details.
    /// 
    /// Error Handling Strategy:
    /// - Validation errors: Publish to erp-invalid-order queue, respond with error
    /// - Transient errors (DB deadlocks, connection issues): Throws to trigger MassTransit retry (3 attempts)
    /// - Business errors (validation, not found): Responds with error, NO retry (prevents wasteful retries)
    /// 
    /// EIP Pattern: Request-Reply with Invalid Message Channel
    /// </summary>
    public class CreateOrderRequestConsumer : IConsumer<CreateOrderRequest>
    {
        private readonly IOrderService _orderService;
        private readonly IValidator<Entities.Model.Order> _orderValidator;
        private readonly ILogger<CreateOrderRequestConsumer> _logger;

        public CreateOrderRequestConsumer(
            IOrderService orderService,
            IValidator<Entities.Model.Order> orderValidator,
            ILogger<CreateOrderRequestConsumer> logger)
        {
            _orderService = orderService;
            _orderValidator = orderValidator;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<CreateOrderRequest> context)
        {
            var request = context.Message;
            
            _logger.LogInformation("Received CreateOrderRequest for Customer {CustomerId} (CorrelationId: {CorrelationId})",
                request.Order.CustomerId, request.CorrelationId);

            // ✅ EARLY VALIDATION - Catch invalid orders before DB operations using FluentValidation
            var validationResult = await _orderValidator.ValidateAsync(request.Order);
            
            if (!validationResult.IsValid)
            {
                var validationErrors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
                
                _logger.LogWarning(
                    "⚠️ [VALIDATION FAILED] Order validation failed for Customer {CustomerId}. Errors: {Errors} (CorrelationId: {CorrelationId})",
                    request.Order.CustomerId,
                    string.Join(", ", validationErrors),
                    request.CorrelationId);

                // Publish OrderInvalid event to erp-invalid-order queue
                await context.Publish(new OrderInvalid
                {
                    OrderId = request.Order.Id,
                    CorrelationId = request.CorrelationId ?? Guid.NewGuid(),
                    InvalidatedAt = DateTime.UtcNow,
                    Reason = "Order validation failed",
                    ValidationErrors = validationErrors,
                    OrderJson = JsonSerializer.Serialize(request.Order)
                });

                // Respond with validation error to client
                await context.RespondAsync(new CreateOrderResponse
                {
                    OrderNo = 0,
                    IsSuccessfullyCreated = false,
                    ErrorMessage = $"Validation failed: {string.Join("; ", validationErrors)}"
                });

                _logger.LogInformation(
                    "Published OrderInvalid event and responded with validation errors (CorrelationId: {CorrelationId})",
                    request.CorrelationId);

                return; // Don't process invalid order further
            }

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
