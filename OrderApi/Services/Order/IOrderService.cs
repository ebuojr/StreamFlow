namespace OrderApi.Services.Order
{
    public interface IOrderService
    {
        Task<int> CreateOrderAsync(Entities.Model.Order order);
    }
}
