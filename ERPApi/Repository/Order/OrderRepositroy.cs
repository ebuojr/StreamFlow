using ERPApi.DBContext;
using Microsoft.EntityFrameworkCore;

namespace ERPApi.Repository.Order
{
    public class OrderRepositroy : IOrderRepository
    {
        private readonly OrderDbContext _context;
        public OrderRepositroy(OrderDbContext context)
        {
            _context = context;
        }

    public async Task<int> CreateOrderAsync(Entities.Model.Order order)
    {
        // NOTE: Transaction is managed by the service layer (for transactional outbox pattern)
        // Generate incremental OrderNo (max + 1)
        var maxOrderNo = await _context.Orders.MaxAsync(o => (int?)o.OrderNo) ?? 0;
        int newOrderNo = maxOrderNo >= 1000 ? (maxOrderNo + 1) : 1000;

        order.OrderNo = newOrderNo;
        _context.Orders.Add(order);
        // NOTE: SaveChanges is called by service layer after adding outbox message

        return newOrderNo;
    }        public async Task<IEnumerable<Entities.Model.Order>> GetAllOrders()
        {
            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                .ToListAsync();
            return orders;
        }

        public async Task<Entities.Model.Order> GetOrderById(Guid id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                throw new System.Collections.Generic.KeyNotFoundException($"Order with id '{id}' was not found.");

            return order;
        }

        public async Task<IEnumerable<Entities.Model.Order>> GetOrderByState(string state)
        {
            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                .Where(o => o.OrderState == state)
                .ToListAsync();
            return orders;
        }

        public async Task<bool> UpdateOrderState(Guid id, string state)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id);
            if (order == null)
                return false;

            order.OrderState = state;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
