namespace ERPApi.Repository.Order
{
    public interface IOrderRepository
    {
        Task<int> CreateOrderAsync(Entities.Model.Order order);
    }
}
