namespace Contracts.Events
{
    /// <summary>
    /// Event published when an order fails validation.
    /// Sent to erp-invalid-order queue for manual review.
    /// </summary>
    public class OrderInvalid
    {
        public Guid OrderId { get; set; }
        public Guid CorrelationId { get; set; }
        public DateTime InvalidatedAt { get; set; }
        public string Reason { get; set; } = string.Empty;
        public List<string> ValidationErrors { get; set; } = new();
        public string OrderJson { get; set; } = string.Empty; // Store original order for review
    }
}
