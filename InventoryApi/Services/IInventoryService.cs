using Entities.Model;

namespace InventoryApi.Services
{
    public interface IInventoryService
    {
        Task<Stock> GetStockBySku(string sku);
        Task UpsertStock(Stock stock);
        Task UpdateStock(Stock stock);
    }
}
