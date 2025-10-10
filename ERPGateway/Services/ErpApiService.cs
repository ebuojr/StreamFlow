using Contracts;
using Entities.Model;
using System.Net.Http.Json;

namespace ERPGateway.Services
{
    public class ErpApiService : IErpApiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ErpApiService> _logger;

        public ErpApiService(HttpClient httpClient, ILogger<ErpApiService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<CreateOrderResponse> CreateOrderAsync(Order order, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Sending order creation request to ERP API for order {OrderId}", order.Id);

                var httpResponse = await _httpClient.PostAsJsonAsync("api/Order", order, cancellationToken);
                
                if (!httpResponse.IsSuccessStatusCode)
                {
                    var errorContent = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("ERP API returned error {StatusCode}: {ErrorContent}", 
                        httpResponse.StatusCode, errorContent);
                    
                    throw new HttpRequestException($"ERP API HTTP {(int)httpResponse.StatusCode} {httpResponse.ReasonPhrase}: {errorContent}");
                }

                var response = await httpResponse.Content.ReadFromJsonAsync<CreateOrderResponse>(cancellationToken: cancellationToken);
                if (response == null)
                {
                    _logger.LogError("ERP API returned null response for order {OrderId}", order.Id);
                    throw new InvalidOperationException("ERP API returned empty response");
                }

                _logger.LogInformation("Successfully created order {OrderNo} in ERP API for order {OrderId}", 
                    response.OrderNo, order.Id);
                
                return response;
            }
            catch (Exception ex) when (ex is not HttpRequestException and not InvalidOperationException)
            {
                _logger.LogError(ex, "Unexpected error calling ERP API for order {OrderId}", order.Id);
                throw new HttpRequestException($"Failed to call ERP API: {ex.Message}", ex);
            }
        }
    }
}