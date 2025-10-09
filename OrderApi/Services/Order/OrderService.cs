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

        public async Task<bool> CreateOrderAsync(Entities.Model.Order order)
        {
            var response = await _client.GetResponse<CreateOrderResponse>(new
            {
                Order = order
            });

            return response.Message.IsSuccessfullyCreated;
        }
    }
}
