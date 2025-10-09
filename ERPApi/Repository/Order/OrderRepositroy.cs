using ERPApi.DBContext;

namespace ERPApi.Repository.Order
{
    public class OrderRepositroy : IOrderRepository
    {
        private readonly OrderDbContext _context;
        public OrderRepositroy(OrderDbContext context)
        {
            _context = context;
        }

        public async Task<bool> CreateOrderAsync(Entities.Model.Order order)
        {
            _context.Orders.Add(order);
            var result = await _context.SaveChangesAsync();
            return result > 0;
        }
    }
}
