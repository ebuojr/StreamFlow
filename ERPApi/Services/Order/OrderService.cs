using Contracts;
using ERPApi.Repository.Order;
using MassTransit;

namespace ERPApi.Services.Order
{
    public class OrderService : IOrderService
    {
        private readonly IOrderRepository orderRepository;
        private readonly ISendEndpointProvider sendEndpointProvider;

        public OrderService(IOrderRepository orderRepository, ISendEndpointProvider sendEndpointProvider)
        {
            this.orderRepository = orderRepository;
            this.sendEndpointProvider = sendEndpointProvider;
        }

        public async Task<int> CreateAndSendOrderAsync(Entities.Model.Order order)
        {
            // need to validate order here
            if (order == null || order.OrderItems == null || !order.OrderItems.Any() ||
                order.Customer == null || order.ShippingAddress == null || order.Payment == null)
                throw new ArgumentException("Order or OrderItems cannot be null or empty.");

            // save order in db 
            var createdOrderNo = await orderRepository.CreateOrderAsync(order);

            // send order to picking service
            await SendOrderToPicking(order);

            return createdOrderNo;
        }

        public async Task<IEnumerable<Entities.Model.Order>> GetAllOrders()
        {
            return await orderRepository.GetAllOrders();
        }

        public async Task<Entities.Model.Order> GetOrderById(Guid id)
        {
            return await orderRepository.GetOrderById(id);
        }

        public Task<IEnumerable<Entities.Model.Order>> GetOrderByState(string state)
        {
            return orderRepository.GetOrderByState(state);
        }

        public async Task<bool> UpdateOrderState(Guid id, string state)
        {
            return await orderRepository.UpdateOrderState(id, state);
        }

        private async Task MarkOrderSentToPicking(int orderNo)
        {
            await orderRepository.MarkOrderSentToPicking(orderNo);
        }

        private async Task<bool> AlreadySentToPicking(int orderNo)
        {
            return await orderRepository.AlreadySentToPicking(orderNo);
        }

        private async Task SendOrderToPicking(Entities.Model.Order order)
        {
            try
            {
                // try to send order picking service here and mark order as sent if successful
                if (!await AlreadySentToPicking(order.OrderNo))
                {
                    // try to send to picking service
                    string queueName = $"{order.FindOrderType()}-picking-queue";
                    var endpoint = await sendEndpointProvider.GetSendEndpoint(new Uri($"queue:{queueName}"));
                    await endpoint.Send(new SendOrderToPicking()
                    {
                        OrderId = order.Id,
                        OrderNo = order.OrderNo,
                        Category = order.FindOrderType(),
                        Timestamp = DateTime.UtcNow
                    });

                    // mark as sent if successful
                    await MarkOrderSentToPicking(order.OrderNo);
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }
    }
}
