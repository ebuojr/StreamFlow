using Contracts;
using ERPGateway.Services;
using MassTransit;

namespace ERPGateway.Messaging
{
    public class CreateOrderRequestConsumer(IErpApiService erpApiService, ILogger<CreateOrderRequestConsumer> logger, IPublishEndpoint publishEndpoint)
        : IConsumer<CreateOrderRequest>
    {
        public async Task Consume(ConsumeContext<CreateOrderRequest> context)
        {
            try
            {
                var request = context.Message;
                logger.LogInformation("Processing CreateOrderRequest for order {OrderId}", request.Order.Id);

                var response = await erpApiService.CreateOrderAsync(request.Order, context.CancellationToken);
                await context.RespondAsync(response);
                
                logger.LogInformation("Successfully processed CreateOrderRequest for order {OrderId}, OrderNo: {OrderNo}", 
                    request.Order.Id, response.OrderNo);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process CreateOrderRequest for order {OrderId}", context.Message.Order.Id);
                
                await publishEndpoint.Publish(new UnhandledOrderByERP
                {
                    Order = context.Message.Order,
                    ErrorMessage = ex.Message,
                    CorrelationId = context.Message.CorrelationId
                }, context.CancellationToken);

                await context.RespondAsync(new CreateOrderResponse
                {
                    OrderNo = 0,
                    IsSuccessfullyCreated = false,
                    ErrorMessage = ex.Message
                });
            }
        }
    }
}
