namespace Contracts.Dtos
{
    public record OrderItemDto
    {
        public string Sku { get; init; } = string.Empty;
        public int Quantity { get; init; }
        public string ProductName { get; init; } = string.Empty;
        public decimal UnitPrice { get; init; }
    }
}
