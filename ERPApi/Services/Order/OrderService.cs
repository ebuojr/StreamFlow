using ERPApi.Repository.Order;

namespace ERPApi.Services.Order
{
    public class OrderService : IOrderService
    {
        private readonly IOrderRepository orderRepository;
        public OrderService(IOrderRepository orderRepository)
        {
            this.orderRepository = orderRepository;
        }
        public async Task<int> CreateAndSendOrderAsync(Entities.Model.Order order)
        {
            var createdOrderNo = await orderRepository.CreateOrderAsync(order);

            // need to send order picking service here

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
    }
}
