using Microsoft.Extensions.AI;

namespace IndexThinking.Abstractions;

/// <summary>
/// Extended token counter with ChatMessage support.
/// Inherits core counting contract from TokenMeter.Abstractions.
/// </summary>
public interface ITokenCounter : TokenMeter.Abstractions.ITokenCounter
{
    /// <summary>
    /// Counts tokens for a chat message (includes role overhead).
    /// </summary>
    /// <param name="message">The chat message to count tokens for.</param>
    /// <returns>The estimated token count including message overhead.</returns>
    int Count(ChatMessage message);
}
