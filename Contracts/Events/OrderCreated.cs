using Contracts.Dtos;

namespace Contracts.Events
{
    /// <summary>
    /// Published when an order is created in ERPApi.
    /// Contains enriched data (Content Enricher pattern) to avoid downstream HTTP calls.
    /// </summary>
    public record OrderCreated
    {
        // Core identifiers
        public Guid OrderId { get; init; }
        public int OrderNo { get; init; }
        public string OrderType { get; init; } = string.Empty; // "Priority" | "Standard"
        public byte Priority { get; init; } // 9 for Priority (DK), 1 for Standard
        
        // Enriched: Order items for inventory check
        public List<OrderItemDto> Items { get; init; } = new();
        
        // Enriched: Customer context
        public CustomerDto Customer { get; init; } = null!;
        
        // Enriched: Shipping context
        public ShippingAddressDto ShippingAddress { get; init; } = null!;
        
        // Enriched: Order summary
        public decimal TotalAmount { get; init; }
        public int TotalItems { get; init; }
        
        // Tracing
        public string CorrelationId { get; init; } = Guid.NewGuid().ToString();
        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    }
}
