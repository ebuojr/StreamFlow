namespace Contracts.DTOs
{
    public class OrderTrackingResponse
    {
        public Guid OrderId { get; set; }
        public string OrderNo { get; set; } = string.Empty;
        public string CurrentState { get; set; } = string.Empty;
        public string StatusMessage { get; set; } = string.Empty;
        public Guid? CorrelationId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUpdatedAt { get; set; }
        public string OrderType { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        
        // Additional tracking info
        public List<OrderStatusHistoryItem> StatusHistory { get; set; } = new();
    }

    public class OrderStatusHistoryItem
    {
        public string State { get; set; } = string.Empty;
        public string StatusMessage { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}
