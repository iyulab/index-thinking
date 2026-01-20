using Microsoft.Extensions.AI;

namespace IndexThinking.Abstractions;

/// <summary>
/// Modifies chat request options to enable reasoning/thinking for providers that require explicit activation.
/// </summary>
/// <remarks>
/// <para>
/// Some LLM providers (DeepSeek, vLLM, GPUStack) require explicit flags in the request
/// to enable reasoning output. This interface abstracts that provider-specific logic.
/// </para>
/// <para>
/// This is the counterpart to <see cref="IReasoningParser"/> - while the parser extracts
/// reasoning from responses, this modifier enables reasoning in requests.
/// </para>
/// </remarks>
public interface IReasoningRequestModifier
{
    /// <summary>
    /// Provider family this modifier handles (e.g., "opensource", "openai", "anthropic").
    /// </summary>
    string ProviderFamily { get; }

    /// <summary>
    /// Determines whether this modifier supports the given model.
    /// </summary>
    /// <param name="modelId">The model identifier.</param>
    /// <returns>True if this modifier can handle the model; otherwise, false.</returns>
    bool SupportsModel(string? modelId);

    /// <summary>
    /// Modifies the chat options to enable reasoning output.
    /// </summary>
    /// <param name="options">The chat options to modify. If null, creates new options.</param>
    /// <param name="modelId">The model identifier for model-specific adjustments.</param>
    /// <returns>The modified chat options with reasoning enabled.</returns>
    ChatOptions EnableReasoning(ChatOptions? options, string? modelId);
}
