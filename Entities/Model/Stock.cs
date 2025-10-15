namespace Entities.Model
{
    public class Stock
    {
        public Guid Id { get; set; }
        public required string Sku { get; set; }
        
        // Total quantity in stock (physical inventory)
        public int TotalQuantity { get; set; }
        
        // Quantity reserved for orders (not yet picked)
        public int ReservedQuantity { get; set; }
        
        // Available quantity (TotalQuantity - ReservedQuantity)
        public int AvailableQuantity => TotalQuantity - ReservedQuantity;
        
        public DateTime LastUpdated { get; set; }
    }
}
