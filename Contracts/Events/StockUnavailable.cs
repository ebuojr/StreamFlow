namespace Contracts.Events
{
    /// <summary>
    /// Published by InventoryApi when stock cannot be reserved for an order.
    /// This is a terminal state - order cannot proceed to picking.
    /// </summary>
    public record StockUnavailable
    {
        public Guid OrderId { get; init; }
        public string CorrelationId { get; init; } = string.Empty;
        public List<string> UnavailableSkus { get; init; } = new();
        public string Reason { get; init; } = string.Empty;
        public DateTime CheckedAt { get; init; } = DateTime.UtcNow;
    }
}
