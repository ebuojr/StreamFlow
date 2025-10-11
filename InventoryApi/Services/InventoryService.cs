using Entities.Model;
using InventoryApi.Repository.Inventory;

namespace InventoryApi.Services
{
    public class InventoryService : IInventoryService
    {
        private readonly IInventoryRepository _inventoryRepository;
        public InventoryService(IInventoryRepository inventoryRepository)
        {
            _inventoryRepository = inventoryRepository;
        }

        public async Task<Stock> GetStockBySku(string sku)
        {
            return await _inventoryRepository.GetStockBySku(sku);
        }

        public async Task UpdateStock(Stock stock)
        {
            await _inventoryRepository.UpdateStock(stock);
        }

        public Task UpsertStock(Stock stock)
        {
            return _inventoryRepository.UpsertStock(stock);
        }
    }
}
