namespace ERPApi.Services.Order
{
    public interface IOrderService
    {
        Task<int> CreateOrderAsync(Entities.Model.Order order);
        Task<Entities.Model.Order> GetOrderById(Guid id);
        Task<IEnumerable<Entities.Model.Order>> GetAllOrders();
        Task<bool> UpdateOrderStatus(Guid id, string status);
    }
}
