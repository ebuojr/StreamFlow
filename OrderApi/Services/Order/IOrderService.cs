namespace OrderApi.Services.Order
{
    public interface IOrderService
    {
        Task<IEnumerable<Entities.Model.Order>> GetOrdersAsync();
        Task<Entities.Model.Order?> GetOrderByIdAsync(Guid id);
        Task<bool> CreateOrderAsync(Entities.Model.Order order);
        Task<bool> RemoveOrderAsync(Entities.Model.Order order);
    }
}
