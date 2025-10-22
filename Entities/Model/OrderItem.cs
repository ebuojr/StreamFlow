namespace Entities.Model
{
    public class OrderItem
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public string? Sku { get; set; }
        public string? Name { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        
        /// <summary>
        /// Tracks the fulfillment status of this order item.
        /// Possible values: Pending, Available, Unavailable, Picked, Packed
        /// </summary>
        public string Status { get; set; } = "Pending";
    }
}
