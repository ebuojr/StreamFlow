using Contracts;
using Entities.Model;

namespace ERPGateway.Services
{
    public interface IErpApiService
    {
        Task<CreateOrderResponse> CreateOrderAsync(Order order, CancellationToken cancellationToken = default);
    }
}