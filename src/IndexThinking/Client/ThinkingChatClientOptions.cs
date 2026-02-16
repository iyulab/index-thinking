using IndexThinking.Agents;
using IndexThinking.Continuation;
using IndexThinking.Context;
using IndexThinking.Core;
using IndexThinking.Modifiers;

namespace IndexThinking.Client;

/// <summary>
/// Configuration options for <see cref="ThinkingChatClient"/>.
/// </summary>
public class ThinkingChatClientOptions
{
    /// <summary>
    /// Default budget configuration for all requests.
    /// Can be overridden per-request via ChatOptions.
    /// </summary>
    public BudgetConfig DefaultBudget { get; set; } = new();

    /// <summary>
    /// Default continuation configuration for handling truncated responses.
    /// </summary>
    public ContinuationConfig DefaultContinuation { get; set; } = ContinuationConfig.Default;

    /// <summary>
    /// Whether to automatically estimate task complexity.
    /// </summary>
    public bool AutoEstimateComplexity { get; set; } = true;

    /// <summary>
    /// Whether to include thinking content in the response metadata.
    /// </summary>
    public bool IncludeThinkingInMetadata { get; set; } = true;

    /// <summary>
    /// Whether to include turn metrics in the response metadata.
    /// </summary>
    public bool IncludeMetricsInMetadata { get; set; } = true;

    /// <summary>
    /// Key used to store session ID in ChatOptions.AdditionalProperties.
    /// </summary>
    public string SessionIdKey { get; set; } = "IndexThinking.SessionId";

    /// <summary>
    /// Factory for generating session IDs when not provided.
    /// Defaults to generating a new GUID.
    /// </summary>
    public Func<string> SessionIdFactory { get; set; } = () => Guid.NewGuid().ToString("N");

    // ========================================
    // Context-Aware Chat Options (v0.9.0)
    // ========================================

    /// <summary>
    /// Whether to enable automatic conversation context tracking.
    /// When enabled, conversations are tracked and context is injected automatically.
    /// Default: true.
    /// </summary>
    public bool EnableContextTracking { get; set; } = true;

    /// <summary>
    /// Whether to enable context injection into messages.
    /// When enabled, previous conversation turns are prepended to requests.
    /// Default: true.
    /// </summary>
    public bool EnableContextInjection { get; set; } = true;

    /// <summary>
    /// Maximum number of previous turns to inject as context.
    /// Default: 5 turns.
    /// </summary>
    public int MaxContextTurns { get; set; } = 5;

    /// <summary>
    /// Context tracker options for session management.
    /// </summary>
    public ContextTrackerOptions ContextTrackerOptions { get; set; } = ContextTrackerOptions.Default;

    /// <summary>
    /// Context injector options for message injection.
    /// </summary>
    public ContextInjectorOptions ContextInjectorOptions { get; set; } = ContextInjectorOptions.Default;

    // ========================================
    // Reasoning Activation Options (v0.12.0)
    // ========================================

    /// <summary>
    /// Whether to explicitly request reasoning content from providers that require it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Some LLM providers (DeepSeek, vLLM, GPUStack, Qwen) require explicit flags in the request
    /// to enable reasoning output. When this option is enabled, IndexThinking automatically
    /// adds the appropriate activation flags based on the detected provider/model.
    /// </para>
    /// <para>
    /// Providers that automatically include reasoning (OpenAI o1/o3/o4, Anthropic Claude, Google Gemini)
    /// are not affected by this setting.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Enable reasoning activation for DeepSeek/vLLM models
    /// var options = new ThinkingChatClientOptions
    /// {
    ///     EnableReasoning = true
    /// };
    /// </code>
    /// </example>
    public bool EnableReasoning { get; set; }

    /// <summary>
    /// Whether to automatically detect if a model requires explicit reasoning activation.
    /// When enabled, reasoning is activated only for models that require it.
    /// When disabled with EnableReasoning=true, reasoning flags are always added.
    /// Default: true.
    /// </summary>
    public bool AutoDetectReasoningRequirement { get; set; } = true;

    /// <summary>
    /// Custom settings for open-source reasoning request modification.
    /// Use this to override default field names or add model-specific settings.
    /// </summary>
    public OpenSourceReasoningRequestSettings? ReasoningRequestSettings { get; set; }
}
