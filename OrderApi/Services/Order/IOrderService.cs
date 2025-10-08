namespace OrderApi.Services.Order
{
    public interface IOrderService
    {
        Task<IEnumerable<Model.Order>> GetOrdersAsync();
        Task<Model.Order?> GetOrderByIdAsync(Guid id);
        Task<bool> CreateOrderAsync(Model.Order order);
        Task<bool> RemoveOrderAsync(Model.Order order);
    }
}
