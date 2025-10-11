namespace ERPApi.Repository.Order
{
    public interface IOrderRepository
    {
        Task<IEnumerable<Entities.Model.Order>> GetAllOrders();
        Task<int> CreateOrderAsync(Entities.Model.Order order);
        Task<IEnumerable<Entities.Model.Order>> GetOrderByState(string state);
        Task<Entities.Model.Order> GetOrderById(Guid id);
        Task<bool> UpdateOrderState(Guid id, string state);
        Task MarkOrderSentToPicking(int orderNo);
        Task<bool> AlreadySentToPicking(int orderNo);
    }
}
