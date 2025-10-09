namespace ERPApi.Services.Order
{
    public interface IOrderService
    {
        Task<bool> CreateOrderAsync(Entities.Model.Order order);
    }
}
