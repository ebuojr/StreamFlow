using System.Security.Cryptography;

namespace Entities.Model
{
    public class Order
    {
        public Guid Id { get; set; }
        public int OrderNo { get; set; }
        public DateTime CreatedAt { get; set; }
        public string OrderState { get; set; } = "Pending";
        public string CountryCode { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public Guid CustomerId { get; set; }
        public string CorrelationId { get; set; } = Guid.NewGuid().ToString(); // For tracking across services
        public List<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        public Customer Customer { get; set; } = null!;
        public Payment Payment { get; set; } = null!;
        public Address ShippingAddress { get; set; } = null!;

        /// <summary>
        /// Determines order type based on shipping country.
        /// DK (Denmark) orders are Priority, all others are Standard.
        /// </summary>
        public string FindOrderType()
        {
            return ShippingAddress?.Country?.Trim().Equals("DK", StringComparison.OrdinalIgnoreCase) == true
                ? "Priority"
                : "Standard";
        }

        /// <summary>
        /// Gets the priority level for RabbitMQ priority queue.
        /// Priority orders (DK) = 9, Standard = 1
        /// </summary>
        public byte GetPriority()
        {
            return FindOrderType() == "Priority" ? (byte)9 : (byte)1;
        }
    }
}
