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
            using var tx = await _context.Database.BeginTransactionAsync();

            var maxOrderNo = await _context.Orders.MaxAsync(o => (int?)o.OrderNo) ?? 0;
            var newOrderNo = maxOrderNo + 1;

            order.OrderNo = newOrderNo;
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            await tx.CommitAsync();
            return newOrderNo;
        }
    }
}
