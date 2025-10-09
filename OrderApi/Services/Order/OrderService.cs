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

        public async Task<int> CreateOrderAsync(Entities.Model.Order order)
        {
            var response = await _client.GetResponse<CreateOrderResponse>(new
            {
                Order = order
            });

            if (response.Message.IsSuccessfullyCreated)
                return response.Message.OrderNo;
            else
                throw new Exception("Order creation failed in ERP system.");
        }
    }
}
