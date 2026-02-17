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
/// Chat completion choice from open-source models.
/// </summary>
public class OpenSourceChoice
{
    /// <summary>
    /// Index of this choice.
    /// </summary>
    [JsonPropertyName("index")]
    public int Index { get; set; }

    /// <summary>
    /// The message content.
    /// </summary>
    [JsonPropertyName("message")]
    public OpenSourceReasoningMessage? Message { get; set; }

    /// <summary>
    /// Delta content for streaming responses.
    /// </summary>
    [JsonPropertyName("delta")]
    public OpenSourceReasoningMessage? Delta { get; set; }

    /// <summary>
    /// Reason why the model stopped generating.
    /// </summary>
    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

/// <summary>
/// Usage information for open-source model responses.
/// </summary>
public class OpenSourceUsage
{
    /// <summary>
    /// Number of input tokens.
    /// </summary>
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    /// <summary>
    /// Number of output tokens.
    /// </summary>
    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    /// <summary>
    /// Total tokens used.
    /// </summary>
    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }

    /// <summary>
    /// Token breakdown details.
    /// </summary>
    [JsonPropertyName("completion_tokens_details")]
    public OpenSourceTokenDetails? CompletionTokensDetails { get; set; }
}

/// <summary>
/// Detailed token breakdown for reasoning.
/// </summary>
public class OpenSourceTokenDetails
{
    /// <summary>
    /// Number of tokens used for reasoning/thinking.
    /// </summary>
    [JsonPropertyName("reasoning_tokens")]
    public int ReasoningTokens { get; set; }
}

/// <summary>
/// Complete chat completion response from open-source models.
/// </summary>
public class OpenSourceChatCompletion
{
    /// <summary>
    /// Unique identifier for the completion.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// Object type (usually "chat.completion").
    /// </summary>
    [JsonPropertyName("object")]
    public string? ObjectType { get; set; }

    /// <summary>
    /// Unix timestamp when the completion was created.
    /// </summary>
    [JsonPropertyName("created")]
    public long Created { get; set; }

    /// <summary>
    /// Model that generated the completion.
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary>
    /// Completion choices.
    /// </summary>
    [JsonPropertyName("choices")]
    public IReadOnlyList<OpenSourceChoice>? Choices { get; set; }

    /// <summary>
    /// Usage information.
    /// </summary>
    [JsonPropertyName("usage")]
    public OpenSourceUsage? Usage { get; set; }
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

/// <summary>
/// Enumeration of supported open-source reasoning model families.
/// </summary>
public static class OpenSourceModelFamilies
{
    /// <summary>DeepSeek R1 and related reasoning models.</summary>
    public const string DeepSeek = "deepseek";

    /// <summary>Qwen models with thinking capabilities (Qwen3, QwQ).</summary>
    public const string Qwen = "qwen";

    /// <summary>vLLM-served models with reasoning_format: parsed.</summary>
    public const string VLLM = "vllm";

    /// <summary>GLM-4.5 and related models.</summary>
    public const string GLM = "glm";
}
