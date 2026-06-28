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

    private int? _maxContextTokens;

    /// <summary>
    /// Maximum context tokens for the model. When set, propagates to both
    /// <see cref="ContinuationConfig.MaxContextTokens"/> and
    /// <see cref="ContextInjectorOptions.MaxContextTokens"/>.
    /// Default: null (uses individual component settings).
    /// </summary>
    public int? MaxContextTokens
    {
        get => _maxContextTokens;
        set
        {
            _maxContextTokens = value;
            if (value.HasValue)
            {
                DefaultContinuation = DefaultContinuation with { MaxContextTokens = value.Value };
                ContextInjectorOptions.MaxContextTokens = value.Value;
            }
        }
    }

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
    public ContextTrackerOptions ContextTrackerOptions { get; set; } = new();

    /// <summary>
    /// Context injector options for message injection.
    /// </summary>
    public ContextInjectorOptions ContextInjectorOptions { get; set; } = new();

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

    /// <summary>
    /// Multiplier applied to MaxOutputTokens on the initial request when reasoning is enabled.
    /// Thinking models (DeepSeek, Qwen3, etc.) consume thinking + content tokens from the same
    /// max_tokens budget. A multiplier of 3 means if MaxOutputTokens is 4096, the initial request
    /// sends max_tokens=12288, giving the model room for both thinking and content output.
    /// The result is still capped by <see cref="ContinuationConfig.MaxContextTokens"/> via
    /// <see cref="ThinkingChatClient.CapMaxOutputTokens"/>.
    /// Default: 2. Set to 1 to disable boosting.
    /// </summary>
    public int ThinkingOutputMultiplier { get; set; } = 2;

    // ========================================
    // Live Streaming Reasoning Separation (v0.20.0)
    // ========================================

    /// <summary>
    /// Whether to separate inline XML-tag reasoning from answer text DURING streaming.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Open-source / local providers (DeepSeek, Qwen3, vLLM-served, local gemma, etc.) emit reasoning
    /// inline in the text delta as <c>&lt;think&gt;…&lt;/think&gt;</c> rather than as a distinct reasoning
    /// channel. When this is enabled, <see cref="ThinkingChatClient.GetStreamingResponseAsync"/> runs each
    /// text delta through an incremental tag state machine (tolerant of tags split across chunk boundaries)
    /// and re-emits reasoning spans as <see cref="Microsoft.Extensions.AI.TextReasoningContent"/> and the
    /// remaining text as <see cref="Microsoft.Extensions.AI.TextContent"/> — so a consumer can render live
    /// "thinking" UI without writing its own stateful tag splitter.
    /// </para>
    /// <para>
    /// Native reasoning providers (OpenAI o-series, Anthropic, Gemini) already emit a separate reasoning
    /// channel and do not carry these tags inline, so enabling this option is a no-op for them (no double
    /// classification). Default: <c>false</c> — preserves the existing raw pass-through (zero regression).
    /// </para>
    /// </remarks>
    public bool SeparateReasoningInStream { get; set; }

    /// <summary>
    /// Opening tag delimiting inline reasoning when <see cref="SeparateReasoningInStream"/> is enabled.
    /// Default: <c>&lt;think&gt;</c> (the open-source/DeepSeek/Qwen convention).
    /// </summary>
    public string StreamingReasoningStartTag { get; set; } = "<think>";

    /// <summary>
    /// Closing tag delimiting inline reasoning when <see cref="SeparateReasoningInStream"/> is enabled.
    /// Default: <c>&lt;/think&gt;</c>.
    /// </summary>
    public string StreamingReasoningEndTag { get; set; } = "</think>";
}
