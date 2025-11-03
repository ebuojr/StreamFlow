using Microsoft.AspNetCore.Mvc;

namespace ERPApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LogsController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<LogsController> _logger;
        private readonly IConfiguration _configuration;

        public LogsController(IHttpClientFactory httpClientFactory, ILogger<LogsController> logger, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _configuration = configuration;
        }

        private HttpClient CreateSeqClient()
        {
            var client = _httpClientFactory.CreateClient();
            var seqApiKey = _configuration["Seq:ApiKey"];

            if (!string.IsNullOrEmpty(seqApiKey))
            {
                if (client.DefaultRequestHeaders.Contains("X-Seq-ApiKey"))
                    client.DefaultRequestHeaders.Remove("X-Seq-ApiKey");

                client.DefaultRequestHeaders.Add("X-Seq-ApiKey", seqApiKey);
            }

            return client;
        }

        [HttpGet("by-orderid/{orderId}")]
        public async Task<IActionResult> GetLogsByOrderId(string orderId, [FromQuery] int count = 100)
        {
            try
            {
                var httpClient = CreateSeqClient();
                var seqBaseUrl = _configuration["Seq:BaseUrl"] ?? "http://localhost:5341";

                var filter = Uri.EscapeDataString($"OrderId='{orderId}'");
                var seqUrl = $"{seqBaseUrl}/api/events?filter={filter}&count={count}&render=false";
                var response = await httpClient.GetAsync(seqUrl);

                if (!response.IsSuccessStatusCode)
                    return StatusCode((int)response.StatusCode, "Failed to fetch logs from Seq");

                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(503, new { error = "Seq service is unavailable", message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }
    }
}
