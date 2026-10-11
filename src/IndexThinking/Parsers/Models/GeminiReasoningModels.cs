using System.Text.Json.Serialization;

namespace IndexThinking.Parsers.Models;

/// <summary>
/// Represents a content part from a Gemini response.
/// Gemini returns content as an array of parts with different types.
/// </summary>
public class GeminiContentPart
{
    /// <summary>
    /// Text content (for text parts).
    /// </summary>
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    /// <summary>
    /// Encrypted thought signature for multi-turn reasoning preservation.
    /// Required for Gemini 3 during function calling, optional for Gemini 2.5.
    /// </summary>
    /// <remarks>
    /// Must be passed back exactly as received in subsequent turns.
    /// Do not modify, compress, or log this value.
    /// </remarks>
    [JsonPropertyName("thoughtSignature")]
    public string? ThoughtSignature { get; set; }

    /// <summary>
    /// Thinking/reasoning text (when thinking mode is enabled).
    /// </summary>
    [JsonPropertyName("thought")]
    public string? Thought { get; set; }

    /// <summary>
    /// Function call (for tool use).
    /// </summary>
    [JsonPropertyName("functionCall")]
    public GeminiFunctionCall? FunctionCall { get; set; }

    /// <summary>
    /// Extra content for OpenAI-compatible endpoints.
    /// Used when accessing Gemini via OpenAI-compatible APIs.
    /// </summary>
    [JsonPropertyName("extra_content")]
    public GeminiExtraContent? ExtraContent { get; set; }
}

/// <summary>
/// Function call details for Gemini tool use.
/// </summary>
public class GeminiFunctionCall
{
    /// <summary>
    /// Name of the function to call.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Arguments for the function call.
    /// </summary>
    [JsonPropertyName("args")]
    public object? Args { get; set; }
}

/// <summary>
/// Extra content structure for OpenAI-compatible Gemini endpoints.
/// </summary>
public class GeminiExtraContent
{
    /// <summary>
    /// Google-specific fields in OpenAI-compatible responses.
    /// </summary>
    [JsonPropertyName("google")]
    public GeminiGoogleContent? Google { get; set; }
}

/// <summary>
/// Google-specific content fields.
/// </summary>
public class GeminiGoogleContent
{
    /// <summary>
    /// Thought signature in OpenAI-compatible format.
    /// Alternative location: extra_content.google.thought_signature
    /// </summary>
    [JsonPropertyName("thought_signature")]
    public string? ThoughtSignature { get; set; }
}
