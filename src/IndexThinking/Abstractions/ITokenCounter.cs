using Microsoft.Extensions.AI;

namespace IndexThinking.Abstractions;

/// <summary>
/// Token counter with ChatMessage support for IndexThinking.
/// </summary>
public interface ITokenCounter
{
    /// <summary>Counts tokens in a text string.</summary>
    int Count(string text);

    /// <summary>Counts tokens across a sequence of text strings.</summary>
    int Count(IEnumerable<string> texts);

    /// <summary>Returns true if this counter supports the given model.</summary>
    bool SupportsModel(string modelId);

    /// <summary>Returns true if this counter produces approximate results for the given model.</summary>
    bool IsApproximate(string modelId) => false;

    /// <summary>
    /// Counts tokens for a chat message (includes role overhead).
    /// </summary>
    /// <param name="message">The chat message to count tokens for.</param>
    /// <returns>The estimated token count including message overhead.</returns>
    int Count(ChatMessage message);
}
