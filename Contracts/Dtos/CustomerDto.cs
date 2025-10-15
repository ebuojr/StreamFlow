namespace Contracts.Dtos
{
    public record CustomerDto
    {
        public Guid CustomerId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string CustomerType { get; init; } = "Regular"; // "Premium" | "Regular"
    }
}
