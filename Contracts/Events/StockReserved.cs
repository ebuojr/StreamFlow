using Contracts.Dtos;

namespace Contracts.Events
{
    /// <summary>
    /// Published by InventoryApi when stock is successfully reserved for an order.
    /// Enriched with all data needed for downstream processing (Content Enricher pattern).
    /// </summary>
    public record StockReserved
    {
        public Guid OrderId { get; init; }
        public string OrderType { get; init; } = string.Empty;
        public string CorrelationId { get; init; } = string.Empty;
        public DateTime ReservedAt { get; init; } = DateTime.UtcNow;
        
        // Enriched data for downstream consumers (no HTTP calls needed)
        public List<OrderItemDto> Items { get; init; } = new();
        public CustomerDto Customer { get; init; } = new();
        public ShippingAddressDto ShippingAddress { get; init; } = new();
    }
}
