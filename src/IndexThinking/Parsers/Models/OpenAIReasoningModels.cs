using System.Text.Json.Serialization;

namespace IndexThinking.Parsers.Models;

/// <summary>
/// Represents an OpenAI Responses API reasoning item.
/// </summary>
/// <remarks>
/// OpenAI's reasoning models (o1, o3, o4) return reasoning tokens that are
/// encrypted for privacy. The summary provides a user-visible description.
/// </remarks>
public sealed record OpenAIReasoningItem
{
    /// <summary>
    /// The type of output item (e.g., "reasoning").
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "reasoning";

    /// <summary>
    /// Unique identifier for this reasoning item.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// Summary of the reasoning process visible to users.
    /// </summary>
    [JsonPropertyName("summary")]
    public IReadOnlyList<OpenAIReasoningSummary>? Summary { get; init; }

    /// <summary>
    /// Encrypted reasoning content for stateless multi-turn conversations.
    /// Only present when <c>include: ["reasoning.encrypted_content"]</c> is specified.
    /// </summary>
    [JsonPropertyName("encrypted_content")]
    public string? EncryptedContent { get; init; }

    /// <summary>
    /// Status of the reasoning (e.g., "completed", "in_progress").
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }
}

/// <summary>
/// Represents a summary item within OpenAI reasoning output.
/// </summary>
public sealed record OpenAIReasoningSummary
{
    /// <summary>
    /// The type of summary content (e.g., "summary_text").
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "summary_text";

    /// <summary>
    /// The summary text content.
    /// </summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }
}

/// <summary>
/// Represents the reasoning configuration for OpenAI requests.
/// </summary>
public sealed record OpenAIReasoningConfig
{
    /// <summary>
    /// The effort level for reasoning (e.g., "low", "medium", "high").
    /// </summary>
    [JsonPropertyName("effort")]
    public string? Effort { get; init; }

    /// <summary>
    /// Whether to generate a reasoning summary.
    /// </summary>
    [JsonPropertyName("generate_summary")]
    public string? GenerateSummary { get; init; }
}

/// <summary>
/// Token usage information for OpenAI reasoning responses.
/// </summary>
public sealed record OpenAIReasoningUsage
{
    /// <summary>
    /// Number of input tokens.
    /// </summary>
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; init; }

    /// <summary>
    /// Number of output tokens (including reasoning).
    /// </summary>
    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; init; }

    /// <summary>
    /// Detailed breakdown of output tokens.
    /// </summary>
    [JsonPropertyName("output_tokens_details")]
    public OpenAIOutputTokenDetails? OutputTokensDetails { get; init; }
}

/// <summary>
/// Detailed breakdown of output tokens.
/// </summary>
public sealed record OpenAIOutputTokenDetails
{
    /// <summary>
    /// Number of tokens used for reasoning.
    /// </summary>
    [JsonPropertyName("reasoning_tokens")]
    public int ReasoningTokens { get; init; }
}
