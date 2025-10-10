using Entities.Model;

namespace Contracts
{
    public record UnhandledOrderByERP
    {
        public Order Order { get; init; } = new();
        public string ErrorMessage { get; init; } = string.Empty;
        public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
        public Guid? CorrelationId { get; init; }
    }
}
