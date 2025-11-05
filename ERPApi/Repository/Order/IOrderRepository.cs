using MassTransit;

namespace ERPApi.Repository.Order
{
    public interface IOrderRepository
    {
        Task<IEnumerable<Entities.Model.Order>> GetAllOrders();
        Task<int> CreateOrderAsync(Entities.Model.Order order);
        Task<IEnumerable<Entities.Model.Order>> GetOrderByState(string state);
        Task<Entities.Model.Order> GetOrderById(Guid id);
        Task<bool> UpdateOrderState(Guid id, string state);
        Task StoreFaultedMessageAsync<T>(Fault<T> fault, int retryCount) where T : class;
        Task<IEnumerable<MassTransit.EntityFrameworkCoreIntegration.OutboxMessage>> GetFaultedMessagesAsync();
    }
}
