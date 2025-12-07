using Contracts.Events;

namespace ERPApi.Services.Order
{
    public interface IOrderService
    {
        /// <summary>
        /// Creates an order in the database and returns the enriched OrderCreated event.
        /// The caller is responsible for publishing the event (to support outbox pattern).
        /// </summary>
        Task<OrderCreated> CreateOrderAsync(Entities.Model.Order order);
        
        Task<Entities.Model.Order> GetOrderById(Guid id);
        Task<IEnumerable<Entities.Model.Order>> GetOrderByState(string state);
        Task<IEnumerable<Entities.Model.Order>> GetAllOrders();
        Task<bool> UpdateOrderState(Guid id, string status);
    }
}
