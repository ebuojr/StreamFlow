namespace ERPApi.Repository.Order
{
    public interface IOrderRepository
    {
        Task<IEnumerable<Entities.Model.Order>> GetAllOrders();
        Task<int> CreateOrderAsync(Entities.Model.Order order);
        Task<Entities.Model.Order> GetOrderById(Guid id);
        Task<bool> UpdateOrderStatus(Guid id, string status);
    }
}
