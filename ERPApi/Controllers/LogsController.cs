using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ERPApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LogsController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<LogsController> _logger;

        public LogsController(IHttpClientFactory httpClientFactory, ILogger<LogsController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        [HttpGet("recent")]
        public async Task<IActionResult> GetRecentLogs([FromQuery] int count = 50)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var seqUrl = $"http://localhost:5341/api/events?count={count}&render=true";
                
                _logger.LogInformation("Fetching recent logs from Seq: {Url}", seqUrl);
                
                var response = await httpClient.GetAsync(seqUrl);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Seq API returned status code: {StatusCode}", response.StatusCode);
                    return StatusCode((int)response.StatusCode, "Failed to fetch logs from Seq");
                }

                var content = await response.Content.ReadAsStringAsync();
                var jsonDocument = JsonDocument.Parse(content);
                
                return Ok(jsonDocument.RootElement);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to connect to Seq API");
                return StatusCode(503, new { error = "Seq service is unavailable", message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching logs from Seq");
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        [HttpGet("by-correlation/{correlationId}")]
        public async Task<IActionResult> GetLogsByCorrelationId(string correlationId, [FromQuery] int count = 50)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var seqUrl = $"http://localhost:5341/api/events?filter=CorrelationId%3D%27{correlationId}%27&count={count}&render=true";
                
                _logger.LogInformation("Fetching logs for CorrelationId {CorrelationId} from Seq", correlationId);
                
                var response = await httpClient.GetAsync(seqUrl);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Seq API returned status code: {StatusCode}", response.StatusCode);
                    return StatusCode((int)response.StatusCode, "Failed to fetch logs from Seq");
                }

                var content = await response.Content.ReadAsStringAsync();
                var jsonDocument = JsonDocument.Parse(content);
                
                return Ok(jsonDocument.RootElement);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to connect to Seq API");
                return StatusCode(503, new { error = "Seq service is unavailable", message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching logs from Seq");
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        [HttpGet("by-order/{orderNo}")]
        public async Task<IActionResult> GetLogsByOrderNo(int orderNo, [FromQuery] int count = 50)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                // Search by OrderNo property in Seq
                var seqUrl = $"http://localhost:5341/api/events?filter=OrderNo%3D{orderNo}&count={count}&render=true";
                
                _logger.LogInformation("Fetching logs for OrderNo {OrderNo} from Seq", orderNo);
                
                var response = await httpClient.GetAsync(seqUrl);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Seq API returned status code: {StatusCode}", response.StatusCode);
                    return StatusCode((int)response.StatusCode, "Failed to fetch logs from Seq");
                }

                var content = await response.Content.ReadAsStringAsync();
                var jsonDocument = JsonDocument.Parse(content);
                
                return Ok(jsonDocument.RootElement);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to connect to Seq API");
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
