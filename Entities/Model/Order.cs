using System.Security.Cryptography;

namespace Entities.Model
{
    public class Order
    {
        public Guid Id { get; set; }
        public int OrderNo { get; set; }
        public DateTime CreatedAt { get; set; }
        public string OrderState { get; set; }
        public string CountryCode { get; set; }
        public bool? IsPreOrder { get; set; }
        public decimal TotalAmount { get; set; }
        public List<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        public Customer Customer { get; set; }
        public Payment Payment { get; set; }
        public Address ShippingAddress { get; set; }

        public string FindOrderType()
        {
            if (IsPreOrder == true)
                return "pre-Order";

            var cc = CountryCode;
            if (!string.IsNullOrWhiteSpace(cc) && string.Equals(cc.Trim(), "DK", StringComparison.OrdinalIgnoreCase))
                return "priority";

            return "standard";
        }
    }
}
