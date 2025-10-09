namespace Entities.Model
{
    public class Order
    {
        public Guid Id { get; set; }
        public string OrderNo { get; set; }
        public DateTime CreatedAt { get; set; }
        public string OrderStatus { get; set; }
        public string CountryCode { get; set; }
        public bool? IsPreOrder { get; set; }
        public decimal TotalAmount { get; set; }
        public List<OrderItem> OrderItems { get; set; }
        public Customer Customer { get; set; }
        public Payment Payment { get; set; }
        public Address ShippingAddress { get; set; }
    }
}
