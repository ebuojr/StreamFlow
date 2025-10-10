using Entities.Model;

namespace Contracts
{
    public record CreateOrderRequest
    {
        public required Order Order { get; set; }
        public Guid? CorrelationId { get; set; }
    }
}
