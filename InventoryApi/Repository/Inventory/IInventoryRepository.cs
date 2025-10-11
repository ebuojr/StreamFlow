using Entities.Model;

namespace InventoryApi.Repository.Inventory
{
    public interface IInventoryRepository
    {
        Task<Stock> GetStockBySku(string sku);
        Task UpsertStock(Stock stock);
        Task UpdateStock(Stock stock);
    }
}
