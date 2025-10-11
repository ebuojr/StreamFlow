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
                var response = await erpApiService.CreateOrderAsync(request.Order, context.CancellationToken);
                await context.RespondAsync(response);
            }
            catch (Exception ex)
            {
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
                    ErrorMessage = ex.Message + ex.InnerException?.Message
                });
            }
        }
    }
}
