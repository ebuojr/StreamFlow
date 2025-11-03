using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorUI.Services;

public class SeqLogService
{
    private readonly HttpClient _httpClient;

    public SeqLogService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        // Use ERPApi as proxy to avoid CORS issues with Seq
        _httpClient.BaseAddress = new Uri("https://localhost:7033");
    }

    public async Task<SeqEventResponse> GetLogsByOrderIdAsync(string orderId, int count = 50)
    {
        try
        {
            // Call ERPApi proxy endpoint to avoid CORS issues
            var response = await _httpClient.GetAsync($"/api/logs/by-orderid/{orderId}?count={count}");

            if (!response.IsSuccessStatusCode)
                return new SeqEventResponse();

            var json = await response.Content.ReadAsStringAsync();

            // ERPApi proxy returns array directly
            var events = JsonSerializer.Deserialize<List<SeqEvent>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return new SeqEventResponse { Events = events ?? new List<SeqEvent>() };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetLogsByOrderIdAsync: {ex.Message}");
            return new SeqEventResponse();
        }
    }
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

    // MessageTemplateTokens for building the rendered message
    [JsonPropertyName("MessageTemplateTokens")]
    public List<MessageTemplateToken> MessageTemplateTokens { get; set; } = new();

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

    public string GetRenderedMessage()
    {
        if (!string.IsNullOrEmpty(RenderedMessage))
            return RenderedMessage;

        // Build message from MessageTemplateTokens if available
        if (MessageTemplateTokens != null && MessageTemplateTokens.Count > 0)
        {
            var messageBuilder = new System.Text.StringBuilder();

            foreach (var token in MessageTemplateTokens)
            {
                if (!string.IsNullOrEmpty(token.Text))
                {
                    // This is a literal text token
                    messageBuilder.Append(token.Text);
                }
                else if (!string.IsNullOrEmpty(token.PropertyName))
                {
                    // This is a property placeholder - get the value from Properties
                    var prop = Properties.FirstOrDefault(p => p.Name == token.PropertyName);
                    if (prop != null && prop.Value != null)
                    {
                        var valueStr = prop.Value is JsonElement jsonElement
                            ? (jsonElement.ValueKind == JsonValueKind.String ? jsonElement.GetString() : jsonElement.ToString())
                            : prop.Value.ToString();
                        messageBuilder.Append(valueStr ?? "");
                    }
                    else
                    {
                        // Fallback to placeholder if property not found
                        messageBuilder.Append($"{{{token.PropertyName}}}");
                    }
                }
            }

            return messageBuilder.ToString();
        }

        // Fallback: Build message from MessageTemplate
        var message = MessageTemplate;
        foreach (var prop in Properties)
        {
            if (prop.Value != null)
            {
                var valueStr = prop.Value is JsonElement jsonElement
                    ? (jsonElement.ValueKind == JsonValueKind.String ? jsonElement.GetString() : jsonElement.ToString())
                    : prop.Value.ToString();

                message = message.Replace($"{{{prop.Name}}}", valueStr ?? "");
            }
        }
        return message;
    }

    public List<SeqProperty> GetRelevantProperties()
    {
        // Filter out common/noisy properties
        var excludedProps = new HashSet<string>
        {
            "Environment", "MachineName", "ThreadId", "Service", "SourceContext",
            "ActionId", "ActionName", "ConnectionId", "RequestId", "Scope", "EventId"
        };

        return Properties.Where(p => !excludedProps.Contains(p.Name)).ToList();
    }
}

public class SeqProperty
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("Value")]
    public object? Value { get; set; }
}

public class MessageTemplateToken
{
    [JsonPropertyName("Text")]
    public string? Text { get; set; }

    [JsonPropertyName("PropertyName")]
    public string? PropertyName { get; set; }
}
