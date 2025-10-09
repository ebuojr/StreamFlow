namespace ERPApi.Repository.Order
{
    public interface IOrderRepository
    {
        Task<bool> CreateOrderAsync(Entities.Model.Order order);
    }
}
