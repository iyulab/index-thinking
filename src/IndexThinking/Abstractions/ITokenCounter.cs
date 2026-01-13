using Microsoft.Extensions.AI;

namespace IndexThinking.Abstractions;

/// <summary>
/// Counts tokens in text for budget management.
/// Supports model-specific tokenizers with fallback chain.
/// </summary>
public interface ITokenCounter
{
    /// <summary>
    /// Counts tokens in the given text.
    /// </summary>
    /// <param name="text">The text to count tokens for.</param>
    /// <returns>The estimated token count.</returns>
    int Count(string text);

    /// <summary>
    /// Counts tokens for a chat message (includes role overhead).
    /// </summary>
    /// <param name="message">The chat message to count tokens for.</param>
    /// <returns>The estimated token count including message overhead.</returns>
    int Count(ChatMessage message);

    /// <summary>
    /// Indicates if this counter supports the specified model.
    /// </summary>
    /// <param name="modelId">The model identifier.</param>
    /// <returns>True if this counter provides accurate counts for the model; otherwise, false.</returns>
    bool SupportsModel(string modelId);
}
