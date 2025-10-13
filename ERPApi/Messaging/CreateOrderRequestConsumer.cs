using Contracts;
using ERPApi.Services.Order;
using MassTransit;

namespace ERPApi.Messaging
{
    public class CreateOrderRequestConsumer(IOrderService orderService, ILogger<CreateOrderRequestConsumer> logger, IPublishEndpoint publishEndpoint)
        : IConsumer<CreateOrderRequest>
    {
        public async Task Consume(ConsumeContext<CreateOrderRequest> context)
        {
            try
            {
                var request = context.Message;
                logger.LogInformation("Processing CreateOrderRequest for Order {OrderId}", request.Order.Id);

                // Create the order using the OrderService
                var orderNo = await orderService.CreateAndSendOrderAsync(request.Order);

                // Respond with success
                await context.RespondAsync(new CreateOrderResponse
                {
                    OrderNo = orderNo,
                    IsSuccessfullyCreated = true,
                    ErrorMessage = string.Empty
                });

                logger.LogInformation("Successfully created order {OrderNo}", orderNo);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing CreateOrderRequest for Order {OrderId}", context.Message.Order.Id);

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
