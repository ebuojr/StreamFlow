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
        public async Task<int> CreateOrderAsync(Entities.Model.Order order)
        {
            return await orderRepository.CreateOrderAsync(order);
        }

        public async Task<IEnumerable<Entities.Model.Order>> GetAllOrders()
        {
            return await orderRepository.GetAllOrders();
        }

        public async Task<Entities.Model.Order> GetOrderById(Guid id)
        {
            return await orderRepository.GetOrderById(id);
        }

        public async Task<bool> UpdateOrderStatus(Guid id, string status)
        {
            return await orderRepository.UpdateOrderStatus(id, status);
        }
    }
}
