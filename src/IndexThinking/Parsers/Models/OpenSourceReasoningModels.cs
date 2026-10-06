using System.Text.Json.Serialization;

namespace IndexThinking.Parsers.Models;

/// <summary>
/// Represents a message with reasoning content from open-source models.
/// Used by DeepSeek, Qwen, and vLLM-served models.
/// </summary>
public class OpenSourceReasoningMessage
{
    /// <summary>
    /// Role of the message (assistant, user, system).
    /// </summary>
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    /// <summary>
    /// Main response content (the final answer).
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    /// <summary>
    /// Chain-of-thought reasoning content (at same level as content).
    /// Contains the model's step-by-step thinking process.
    /// </summary>
    /// <remarks>
    /// For DeepSeek models, this contains content that would be within
    /// &lt;think&gt;...&lt;/think&gt; tags in the raw output.
    /// </remarks>
    [JsonPropertyName("reasoning_content")]
    public string? ReasoningContent { get; set; }

    /// <summary>
    /// Alternative name for reasoning content used by vLLM.
    /// </summary>
    [JsonPropertyName("reasoning")]
    public string? Reasoning { get; set; }

    /// <summary>
    /// Tool/function calls if requested.
    /// </summary>
    [JsonPropertyName("tool_calls")]
    public IReadOnlyList<OpenSourceToolCall>? ToolCalls { get; set; }
}

/// <summary>
/// Represents a tool call from open-source models.
/// </summary>
public class OpenSourceToolCall
{
    /// <summary>
    /// Unique identifier for the tool call.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// Type of tool call (usually "function").
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// Function call details.
    /// </summary>
    [JsonPropertyName("function")]
    public OpenSourceFunctionCall? Function { get; set; }
}

/// <summary>
/// Function call details for open-source models.
/// </summary>
public class OpenSourceFunctionCall
{
    /// <summary>
    /// Name of the function to call.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// JSON-encoded arguments for the function.
    /// </summary>
    [JsonPropertyName("arguments")]
    public string? Arguments { get; set; }
}

/// <summary>
/// Configuration for thinking mode in DeepSeek models.
/// </summary>
public class DeepSeekThinkingConfig
{
    /// <summary>
    /// Whether to enable thinking mode.
    /// When true, responses will include reasoning_content.
    /// </summary>
    public bool EnableThinking { get; set; } = true;

    /// <summary>
    /// Start token for thinking (default: "&lt;think&gt;").
    /// Used to identify the beginning of reasoning content.
    /// </summary>
    public string StartToken { get; set; } = "<think>";

    /// <summary>
    /// End token for thinking (default: "&lt;/think&gt;").
    /// Used to identify the end of reasoning content.
    /// </summary>
    public string EndToken { get; set; } = "</think>";
}
