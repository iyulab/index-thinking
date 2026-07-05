using Microsoft.Extensions.AI;

namespace IndexThinking.Abstractions;

/// <summary>
/// Token counter extension for Microsoft.Extensions.AI consumers. Splits the
/// <see cref="ChatMessage"/> member out of <see cref="ITokenCounter"/> so the core
/// counting contract stays framework-neutral.
/// </summary>
public interface IChatMessageTokenCounter : ITokenCounter
{
    /// <summary>
    /// Counts tokens for a chat message (includes role overhead).
    /// </summary>
    /// <param name="message">The chat message to count tokens for.</param>
    /// <returns>The estimated token count including message overhead.</returns>
    int Count(ChatMessage message);
}
