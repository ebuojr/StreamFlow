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
        public async Task<bool> CreateOrderAsync(Entities.Model.Order order)
        {
            return await orderRepository.CreateOrderAsync(order);
        }
    }
}
