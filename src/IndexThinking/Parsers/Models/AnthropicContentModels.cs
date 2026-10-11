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
