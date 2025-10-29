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
            var correlationId = request.CorrelationId?.ToString() ?? Guid.NewGuid().ToString();
            
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["CustomerId"] = request.Order.CustomerId
            }))
            {
                _logger.LogInformation("[ERP-Api] CreateOrderRequest received. CustomerId={CustomerId}",
                    request.Order.CustomerId);

                var validationResult = await _orderValidator.ValidateAsync(request.Order);
            
            if (!validationResult.IsValid)
            {
                var validationErrors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
                
                _logger.LogWarning("[ERP-Api] Order validation failed. CustomerId={CustomerId}, Errors={Errors}",
                    request.Order.CustomerId,
                    string.Join(", ", validationErrors));

                await context.Publish(new OrderInvalid
                {
                    OrderId = request.Order.Id,
                    CorrelationId = request.CorrelationId ?? Guid.NewGuid(),
                    InvalidatedAt = DateTime.UtcNow,
                    Reason = "Order validation failed",
                    ValidationErrors = validationErrors,
                    OrderJson = JsonSerializer.Serialize(request.Order)
                });

                await context.RespondAsync(new CreateOrderResponse
                {
                    OrderNo = 0,
                    IsSuccessfullyCreated = false,
                    ErrorMessage = $"Validation failed: {string.Join("; ", validationErrors)}"
                });

                _logger.LogInformation("[ERP-Api] OrderInvalid event published.");

                return;
            }

            try
            {
                var orderNo = await _orderService.CreateAndSendOrderAsync(request.Order);

                _logger.LogInformation("[ERP-Api] Order created. OrderNo={OrderNo}, CustomerId={CustomerId}",
                    orderNo, request.Order.CustomerId);

                await context.RespondAsync(new CreateOrderResponse
                {
                    OrderNo = orderNo,
                    IsSuccessfullyCreated = true,
                    ErrorMessage = string.Empty
                });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "[ERP-Api] Transient DB error. CustomerId={CustomerId}, Will retry.",
                    request.Order.CustomerId);
                
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ERP-Api] Business error. CustomerId={CustomerId}, No retry.",
                    request.Order.CustomerId);

                await context.RespondAsync(new CreateOrderResponse
                {
                    OrderNo = 0,
                    IsSuccessfullyCreated = false,
                    ErrorMessage = ex.Message + (ex.InnerException != null ? " | " + ex.InnerException.Message : string.Empty)
                });
            }
            }
        }
    }
}
