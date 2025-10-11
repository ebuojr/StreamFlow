namespace Entities.Model
{
    public class Stock
    {
        public Guid Id { get; set; }
        public string Sku { get; set; }
        public int Quantity { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
