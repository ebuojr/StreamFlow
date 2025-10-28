using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorUI.Services;

public class SeqLogService
{
    private readonly HttpClient _httpClient;

    public SeqLogService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        // Use ERPApi as a proxy to avoid CORS issues
        _httpClient.BaseAddress = new Uri("https://localhost:7033");
    }

    public async Task<SeqEventResponse> GetRecentLogsAsync(int count = 50)
    {
        try
        {
            // Call ERPApi proxy endpoint instead of Seq directly
            var response = await _httpClient.GetAsync($"/api/logs/recent?count={count}");
            
            if (!response.IsSuccessStatusCode)
                return new SeqEventResponse();

            var json = await response.Content.ReadAsStringAsync();
            
            // Seq API with render=true wraps the array in a "value" property
            var seqResponse = JsonSerializer.Deserialize<SeqApiResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return new SeqEventResponse { Events = seqResponse?.Value ?? new List<SeqEvent>() };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetRecentLogsAsync: {ex.Message}");
            return new SeqEventResponse();
        }
    }

    public async Task<SeqEventResponse> GetLogsByCorrelationIdAsync(string correlationId, int count = 50)
    {
        try
        {
            // Call ERPApi proxy endpoint
            var response = await _httpClient.GetAsync($"/api/logs/by-correlation/{correlationId}?count={count}");
            
            if (!response.IsSuccessStatusCode)
                return new SeqEventResponse();

            var json = await response.Content.ReadAsStringAsync();
            
            // Seq API with render=true wraps the array in a "value" property
            var seqResponse = JsonSerializer.Deserialize<SeqApiResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return new SeqEventResponse { Events = seqResponse?.Value ?? new List<SeqEvent>() };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetLogsByCorrelationIdAsync: {ex.Message}");
            return new SeqEventResponse();
        }
    }

    public async Task<SeqEventResponse> GetLogsByOrderNoAsync(int orderNo, int count = 50)
    {
        try
        {
            // Call ERPApi proxy endpoint for OrderNo search
            var response = await _httpClient.GetAsync($"/api/logs/by-order/{orderNo}?count={count}");
            
            if (!response.IsSuccessStatusCode)
                return new SeqEventResponse();

            var json = await response.Content.ReadAsStringAsync();
            
            // Seq API with render=true wraps the array in a "value" property
            var seqResponse = JsonSerializer.Deserialize<SeqApiResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return new SeqEventResponse { Events = seqResponse?.Value ?? new List<SeqEvent>() };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetLogsByOrderNoAsync: {ex.Message}");
            return new SeqEventResponse();
        }
    }
}

// Seq API response wrapper when render=true
public class SeqApiResponse
{
    [JsonPropertyName("value")]
    public List<SeqEvent> Value { get; set; } = new();

    [JsonPropertyName("Count")]
    public int Count { get; set; }
}

public class SeqEventResponse
{
    [JsonPropertyName("Events")]
    public List<SeqEvent> Events { get; set; } = new();
}

public class SeqEvent
{
    [JsonPropertyName("Timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("Level")]
    public string Level { get; set; } = string.Empty;

    [JsonPropertyName("MessageTemplate")]
    public string MessageTemplate { get; set; } = string.Empty;

    [JsonPropertyName("RenderedMessage")]
    public string RenderedMessage { get; set; } = string.Empty;

    // Seq returns Properties as an array of {Name, Value} objects
    [JsonPropertyName("Properties")]
    public List<SeqProperty> Properties { get; set; } = new();

    public string GetProperty(string key)
    {
        var prop = Properties.FirstOrDefault(p => p.Name == key);
        if (prop != null && prop.Value != null)
        {
            // Handle JsonElement from Value property
            if (prop.Value is JsonElement jsonElement)
            {
                return jsonElement.ValueKind == JsonValueKind.String 
                    ? jsonElement.GetString() ?? string.Empty 
                    : jsonElement.ToString();
            }
            return prop.Value.ToString() ?? string.Empty;
        }
        return string.Empty;
    }
}

public class SeqProperty
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("Value")]
    public object? Value { get; set; }
}
