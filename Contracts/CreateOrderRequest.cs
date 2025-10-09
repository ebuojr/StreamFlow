using Entities.Model;

namespace Contracts
{
    public record CreateOrderRequest
    {
        public Order Order { get; set; }
    }
}
