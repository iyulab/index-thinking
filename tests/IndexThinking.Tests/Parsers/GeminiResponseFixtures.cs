using System.Text.Json.Serialization;
using IndexThinking.Parsers.Models;

namespace IndexThinking.Tests.Parsers;

// Native Gemini response shape, used only to build raw-representation fixtures.
// The parser reads the JSON itself; it never deserializes into these types.

/// <summary>
/// Gemini API response structure.
/// </summary>
internal sealed class GeminiResponse
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
/// Represents a Gemini API response candidate.
/// </summary>
internal sealed class GeminiCandidate
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
internal sealed class GeminiContent
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
/// Usage metadata for Gemini responses.
/// </summary>
internal sealed class GeminiUsageMetadata
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
