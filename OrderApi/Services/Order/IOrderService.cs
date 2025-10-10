using Contracts;

namespace OrderApi.Services.Order
{
    public interface IOrderService
    {
        Task<CreateOrderResponse> SendOrderToERP(Entities.Model.Order order);
    }
}
