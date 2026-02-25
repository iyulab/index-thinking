using System.Text.Json.Serialization;

namespace IndexThinking.Parsers.Models;

/// <summary>
/// Base class for Anthropic response content blocks.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(AnthropicThinkingBlock), "thinking")]
[JsonDerivedType(typeof(AnthropicRedactedThinkingBlock), "redacted_thinking")]
[JsonDerivedType(typeof(AnthropicTextBlock), "text")]
public abstract record AnthropicContentBlock
{
    /// <summary>
    /// The type of content block.
    /// </summary>
    [JsonIgnore]
    public abstract string Type { get; }
}

/// <summary>
/// Represents a thinking block from Anthropic's Extended Thinking feature.
/// </summary>
/// <remarks>
/// Thinking blocks contain Claude's internal reasoning process.
/// The signature field is used to verify authenticity when passing back to the API.
/// </remarks>
public sealed record AnthropicThinkingBlock : AnthropicContentBlock
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Type => "thinking";

    /// <summary>
    /// The thinking/reasoning text content.
    /// </summary>
    [JsonPropertyName("thinking")]
    public required string Thinking { get; init; }

    /// <summary>
    /// Cryptographic signature for verification.
    /// This field is opaque and must be preserved exactly when passing back to the API.
    /// </summary>
    [JsonPropertyName("signature")]
    public required string Signature { get; init; }
}

/// <summary>
/// Represents a redacted thinking block.
/// </summary>
/// <remarks>
/// Redacted thinking blocks appear when content has been filtered for safety.
/// The data field contains opaque encrypted content.
/// </remarks>
public sealed record AnthropicRedactedThinkingBlock : AnthropicContentBlock
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Type => "redacted_thinking";

    /// <summary>
    /// Opaque encrypted data for the redacted thinking.
    /// </summary>
    [JsonPropertyName("data")]
    public required string Data { get; init; }
}

/// <summary>
/// Represents a standard text content block.
/// </summary>
public sealed record AnthropicTextBlock : AnthropicContentBlock
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Type => "text";

    /// <summary>
    /// The text content.
    /// </summary>
    [JsonPropertyName("text")]
    public required string Text { get; init; }
}

/// <summary>
/// Represents the thinking configuration for Anthropic requests.
/// </summary>
public sealed record AnthropicThinkingConfig
{
    /// <summary>
    /// Whether extended thinking is enabled.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "enabled";

    /// <summary>
    /// Maximum token budget for thinking.
    /// Minimum is 1024 tokens.
    /// </summary>
    [JsonPropertyName("budget_tokens")]
    public int BudgetTokens { get; init; } = 10000;
}

/// <summary>
/// Token usage information for Anthropic responses.
/// </summary>
public sealed record AnthropicUsage
{
    /// <summary>
    /// Number of input tokens.
    /// </summary>
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; init; }

    /// <summary>
    /// Number of output tokens.
    /// </summary>
    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; init; }

    /// <summary>
    /// Number of tokens used for thinking (extended thinking only).
    /// </summary>
    [JsonPropertyName("thinking_tokens")]
    public int? ThinkingTokens { get; init; }

    /// <summary>
    /// Number of input tokens read from cache.
    /// </summary>
    [JsonPropertyName("cache_read_input_tokens")]
    public int? CacheReadInputTokens { get; init; }

    /// <summary>
    /// Number of input tokens written to cache.
    /// </summary>
    [JsonPropertyName("cache_creation_input_tokens")]
    public int? CacheCreationInputTokens { get; init; }
}

/// <summary>
/// Represents an Anthropic message response.
/// </summary>
public sealed record AnthropicMessageResponse
{
    /// <summary>
    /// The response content blocks.
    /// </summary>
    [JsonPropertyName("content")]
    public IReadOnlyList<AnthropicContentBlock>? Content { get; init; }

    /// <summary>
    /// The model that generated the response.
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    /// <summary>
    /// The reason the response stopped.
    /// </summary>
    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; init; }

    /// <summary>
    /// Token usage information.
    /// </summary>
    [JsonPropertyName("usage")]
    public AnthropicUsage? Usage { get; init; }
}
