using Contracts.Dtos;

namespace Contracts.Events
{
    /// <summary>
    /// Published by PickingService when an order has been picked.
    /// Enriched with all data needed for downstream processing (Content Enricher pattern).
    /// </summary>
    public record OrderPicked
    {
        public Guid OrderId { get; init; }
        public string OrderType { get; init; } = string.Empty;
        public string CorrelationId { get; init; } = string.Empty;
        public DateTime PickedAt { get; init; } = DateTime.UtcNow;
        public string PickedBy { get; init; } = "System"; // Could be worker ID
        
        // Enriched data for downstream consumers (no HTTP calls needed)
        public List<OrderItemDto> Items { get; init; } = new();
        public CustomerDto Customer { get; init; } = new();
        public ShippingAddressDto ShippingAddress { get; init; } = new();
    }
}
