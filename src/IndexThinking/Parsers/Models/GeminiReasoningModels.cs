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

/// <summary>
/// Represents a Gemini API response candidate.
/// </summary>
public class GeminiCandidate
{
    /// <summary>
    /// Content object containing parts.
    /// </summary>
    [JsonPropertyName("content")]
    public GeminiContent? Content { get; set; }

    /// <summary>
    /// Reason why the model stopped generating.
    /// </summary>
    [JsonPropertyName("finishReason")]
    public string? FinishReason { get; set; }
}

/// <summary>
/// Content object with parts array.
/// </summary>
public class GeminiContent
{
    /// <summary>
    /// Role of the content (model, user, etc.).
    /// </summary>
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    /// <summary>
    /// Parts of the content.
    /// </summary>
    [JsonPropertyName("parts")]
    public IReadOnlyList<GeminiContentPart>? Parts { get; set; }
}

/// <summary>
/// Gemini API response structure.
/// </summary>
public class GeminiResponse
{
    /// <summary>
    /// Response candidates.
    /// </summary>
    [JsonPropertyName("candidates")]
    public IReadOnlyList<GeminiCandidate>? Candidates { get; set; }

    /// <summary>
    /// Usage metadata for the response.
    /// </summary>
    [JsonPropertyName("usageMetadata")]
    public GeminiUsageMetadata? UsageMetadata { get; set; }
}

/// <summary>
/// Usage metadata for Gemini responses.
/// </summary>
public class GeminiUsageMetadata
{
    /// <summary>
    /// Number of input tokens.
    /// </summary>
    [JsonPropertyName("promptTokenCount")]
    public int PromptTokenCount { get; set; }

    /// <summary>
    /// Number of output tokens.
    /// </summary>
    [JsonPropertyName("candidatesTokenCount")]
    public int CandidatesTokenCount { get; set; }

    /// <summary>
    /// Total tokens used.
    /// </summary>
    [JsonPropertyName("totalTokenCount")]
    public int TotalTokenCount { get; set; }

    /// <summary>
    /// Number of thinking/reasoning tokens used.
    /// </summary>
    [JsonPropertyName("thoughtsTokenCount")]
    public int ThoughtsTokenCount { get; set; }
}

/// <summary>
/// Thinking configuration for Gemini requests.
/// </summary>
public class GeminiThinkingConfig
{
    /// <summary>
    /// Thinking level for Gemini 3+ models.
    /// Values: "minimal", "low", "medium", "high" (Flash) or "low", "high" (Pro).
    /// </summary>
    [JsonPropertyName("thinkingLevel")]
    public string? ThinkingLevel { get; set; }

    /// <summary>
    /// Legacy thinking budget for Gemini 2.5 models.
    /// Number of tokens allocated for thinking (deprecated in Gemini 3).
    /// </summary>
    [JsonPropertyName("thinkingBudget")]
    public int? ThinkingBudget { get; set; }

    /// <summary>
    /// Whether thinking is enabled.
    /// </summary>
    [JsonPropertyName("includeThoughts")]
    public bool IncludeThoughts { get; set; } = true;
}
