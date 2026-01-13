using IndexThinking.Core;
using Microsoft.Extensions.AI;

namespace IndexThinking.Abstractions;

/// <summary>
/// Parses reasoning/thinking content from LLM responses.
/// Each provider has different formats (thinking blocks, reasoning tokens, XML tags).
/// </summary>
public interface IReasoningParser
{
    /// <summary>
    /// Provider family this parser handles (e.g., "openai", "anthropic", "gemini").
    /// </summary>
    string ProviderFamily { get; }

    /// <summary>
    /// Attempts to parse thinking content from a chat response.
    /// </summary>
    /// <param name="response">The chat response to parse.</param>
    /// <param name="content">The parsed thinking content, if found.</param>
    /// <returns>True if thinking content was found and parsed; otherwise, false.</returns>
    bool TryParse(ChatResponse response, out ThinkingContent? content);

    /// <summary>
    /// Extracts provider-specific state to preserve for multi-turn conversations.
    /// </summary>
    /// <param name="response">The chat response to extract state from.</param>
    /// <returns>The reasoning state if available; otherwise, null.</returns>
    ReasoningState? ExtractState(ChatResponse response);
}
