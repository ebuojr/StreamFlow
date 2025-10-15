using Contracts.Dtos;

namespace Contracts.Events
{
    /// <summary>
    /// Published by InventoryService when only some items are available (partial fulfillment).
    /// Contains available items for processing and unavailable items for tracking.
    /// </summary>
    public record PartialStockReserved
    {
        public Guid OrderId { get; init; }
        public int OrderNo { get; init; }
        public string OrderType { get; init; } = string.Empty;
        public string CorrelationId { get; init; } = string.Empty;
        public DateTime ReservedAt { get; init; } = DateTime.UtcNow;
        
        // Items that are available and reserved for processing
        public List<OrderItemDto> AvailableItems { get; init; } = new();
        
        // Items that are out of stock
        public List<OrderItemDto> UnavailableItems { get; init; } = new();
        
        // Enriched data for downstream consumers
        public CustomerDto Customer { get; init; } = new();
        public ShippingAddressDto ShippingAddress { get; init; } = new();
    }
}
