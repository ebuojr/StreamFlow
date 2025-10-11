namespace Contracts
{
    public class SendOrderToPicking
    {
        public Guid OrderId { get; init; }
        public int OrderNo { get; init; }
        public string Category { get; init; } = default!; // "preorder" | "priority" | "standard"
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    }
}
