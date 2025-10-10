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

        public async Task<int> SendOrderToERP(Entities.Model.Order order)
        {
            var correlationId = Guid.NewGuid();
            var response = await _client.GetResponse<CreateOrderResponse>(new CreateOrderRequest
            {
                Order = order,
                CorrelationId = correlationId
            });

            if (!response.Message.IsSuccessfullyCreated)
                throw new InvalidOperationException($"Order creation failed: {response.Message.ErrorMessage}");
            return response.Message.OrderNo;
        }
    }
}
