using Contracts.Dtos;

namespace Contracts.Events
{
    /// <summary>
    /// Published by PackingService when an order has been packed.
    /// This is the final event in the order processing flow.
    /// </summary>
    public record OrderPacked
    {
        public Guid OrderId { get; init; }
        public string CorrelationId { get; init; } = string.Empty;
        public DateTime PackedAt { get; init; } = DateTime.UtcNow;
        public string PackedBy { get; init; } = "System"; // Could be worker ID
        public decimal TotalWeight { get; init; } // For shipping
        public string BoxSize { get; init; } = "Standard"; // "Small" | "Standard" | "Large"
        
        // Enriched data (minimal - mainly for tracking/logging)
        public List<OrderItemDto> Items { get; init; } = new();
        public ShippingAddressDto ShippingAddress { get; init; } = new();
    }
}
