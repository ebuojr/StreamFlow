namespace OrderApi.Services.Order
{
    public interface IOrderService
    {
        Task<int> SendOrderToERP(Entities.Model.Order order);
    }
}
