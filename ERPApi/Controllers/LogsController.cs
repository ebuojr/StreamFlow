using ERPApi.Configuration;
using Microsoft.AspNetCore.Mvc;

namespace ERPApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LogsController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<LogsController> _logger;
        private readonly SeqSettings _seqSettings;

        public LogsController(IHttpClientFactory httpClientFactory, ILogger<LogsController> logger, SeqSettings seqSettings)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _seqSettings = seqSettings ?? throw new ArgumentNullException(nameof(seqSettings));
        }

        [HttpGet("by-orderid/{orderId}")]
        public async Task<IActionResult> GetLogsByOrderId(string orderId, [FromQuery] int count = 100)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                
                // Add Seq API key header
                if (!string.IsNullOrEmpty(_seqSettings.ApiKey))
                {
                    httpClient.DefaultRequestHeaders.Add("X-Seq-ApiKey", _seqSettings.ApiKey);
                }

                var filter = Uri.EscapeDataString($"OrderId='{orderId}'");
                var seqUrl = $"{_seqSettings.BaseUrl}/api/events?filter={filter}&count={count}&render=false";
                
                _logger.LogDebug("Fetching logs from Seq: {SeqUrl}", seqUrl);
                
                var response = await httpClient.GetAsync(seqUrl);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to fetch logs from Seq. Status: {StatusCode}", response.StatusCode);
                    return StatusCode((int)response.StatusCode, "Failed to fetch logs from Seq");
                }

                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Seq service is unavailable");
                return StatusCode(503, new { error = "Seq service is unavailable", message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching logs from Seq");
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }
    }
}
