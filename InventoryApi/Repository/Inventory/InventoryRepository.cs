using Entities.Model;
using InventoryApi.DBContext;
using Microsoft.EntityFrameworkCore;

namespace InventoryApi.Repository.Inventory
{
    public class InventoryRepository : IInventoryRepository
    {
        private readonly InventoryDbContext _context;
        public InventoryRepository(InventoryDbContext context)
        {
            _context = context;
        }

        public async Task<Stock> GetStockBySku(string sku)
        {
            return await _context.Stocks.FirstOrDefaultAsync(s => s.Sku == sku) ?? new Stock();
        }

        public async Task UpdateStock(Stock stock)
        {
            using var tx = await _context.Database.BeginTransactionAsync();

            _context.Stocks.Update(stock);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
        }

        public async Task UpsertStock(Stock stock)
        {
            using var tx = await _context.Database.BeginTransactionAsync();
            var existingStock = await _context.Stocks.FirstOrDefaultAsync(s => s.Sku == stock.Sku);
            if (existingStock != null)
            {
                // Update existing stock
                existingStock.Quantity = stock.Quantity;
                existingStock.LastUpdated = DateTime.UtcNow;
                _context.Stocks.Update(existingStock);
            }
            else
            {
                // Insert new stock
                stock.Id = Guid.NewGuid();
                stock.LastUpdated = DateTime.UtcNow;
                _context.Stocks.Add(stock);
            }
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
        }
    }
}
