using Contracts;
using Entities.Model;
using ERPApi.Services.Order;
using MassTransit;

namespace ERPApi.Messaging
{
    public class CreateOrderRequestConsumer(IOrderService orderService, IPublishEndpoint publishEndpoint)
        : IConsumer<CreateOrderRequest>
    {
        public async Task Consume(ConsumeContext<CreateOrderRequest> context)
        {
            try
            {
                var request = context.Message;
                var orderNo = await orderService.CreateAndSendOrderAsync(request.Order);

                // Respond with success
                await context.RespondAsync(new CreateOrderResponse
                {
                    OrderNo = orderNo,
                    IsSuccessfullyCreated = true,
                    ErrorMessage = string.Empty
                });

                // Publish to outbox
                await publishEndpoint.Publish(new Outbox()
                {
                    ProcessedAt = DateTime.Now,
                    MessageType = nameof(CreateOrderRequest),
                    Payload = System.Text.Json.JsonSerializer.Serialize(request),
                    CreatedAt = DateTime.UtcNow,
                    RetryCount = 0
                });
            }
            catch (Exception ex)
            {
                // Publish to unhandled-orders queue
                await publishEndpoint.Publish(new UnhandledOrderByERP
                {
                    Order = context.Message.Order,
                    ErrorMessage = ex.Message,
                    CorrelationId = context.Message.CorrelationId
                }, context.CancellationToken);

                // Respond with failure
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
