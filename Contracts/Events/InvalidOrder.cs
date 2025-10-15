namespace Contracts.Events
{
    /// <summary>
    /// Published when an order fails validation in ERPApi.
    /// Routed to invalid-orders queue for manual review.
    /// </summary>
    public record InvalidOrder
    {
        public Guid OrderId { get; init; }
        public string Reason { get; init; } = string.Empty;
        public List<string> ValidationErrors { get; init; } = new();
        public string RawPayload { get; init; } = string.Empty;
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public string CorrelationId { get; init; } = string.Empty;
    }
}
