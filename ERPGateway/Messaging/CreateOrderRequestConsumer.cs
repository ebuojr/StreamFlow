using Contracts;
using MassTransit;
using System.Net.Http.Json;

namespace ERPGateway.Messaging
{
    public class CreateOrderRequestConsumer(HttpClient httpClient, ILogger<CreateOrderRequestConsumer> logger, IPublishEndpoint publishEndpoint)
        : IConsumer<CreateOrderRequest>
    {
        public async Task Consume(ConsumeContext<CreateOrderRequest> context)
        {
            try
            {
                var request = context.Message;

                var httpResponse = await httpClient.PostAsJsonAsync("api/Order", request.Order, context.CancellationToken);
                if (!httpResponse.IsSuccessStatusCode)
                {
                    var error = $"ERPApi HTTP {(int)httpResponse.StatusCode} {httpResponse.ReasonPhrase}";
                    await publishEndpoint.Publish(new UnhandledOrderByERP
                    {
                        Order = request.Order,
                        ErrorMessage = error,
                        CorrelationId = request.CorrelationId
                    }, context.CancellationToken);

                    await context.RespondAsync(new CreateOrderResponse
                    {
                        OrderNo = 0,
                        IsSuccessfullyCreated = false,
                        ErrorMessage = error
                    });

                    return;
                }

                var payload = await httpResponse.Content.ReadFromJsonAsync<CreateOrderResponse>(cancellationToken: context.CancellationToken);
                if (payload == null)
                {
                    var error = "ERPApi returned empty response";
                    await publishEndpoint.Publish(new UnhandledOrderByERP
                    {
                        Order = request.Order,
                        ErrorMessage = error,
                        CorrelationId = request.CorrelationId
                    }, context.CancellationToken);

                    await context.RespondAsync(new CreateOrderResponse
                    {
                        OrderNo = 0,
                        IsSuccessfullyCreated = false,
                        ErrorMessage = error
                    });
                    return;
                }

                await context.RespondAsync(payload);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process CreateOrderRequest");
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
