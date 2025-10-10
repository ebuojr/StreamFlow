using Contracts;
using MassTransit;

namespace OrderApi.Services.Order
{
    public class OrderService : IOrderService
    {
        private readonly IRequestClient<CreateOrderRequest> _client;

        public OrderService(IRequestClient<CreateOrderRequest> client)
        {
            _client = client;
        }

        public async Task<CreateOrderResponse> SendOrderToERP(Entities.Model.Order order)
        {
            var correlationId = order.Id;
            var response = await _client.GetResponse<CreateOrderResponse>(new CreateOrderRequest
            {
                Order = order,
                CorrelationId = correlationId
            });

            return response.Message;
        }
    }
}
