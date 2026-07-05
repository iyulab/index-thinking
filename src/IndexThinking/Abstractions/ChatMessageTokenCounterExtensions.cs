using Microsoft.Extensions.AI;

namespace IndexThinking.Abstractions;

/// <summary>
/// Bridges any <see cref="ITokenCounter"/> to chat-message counting: delegates to
/// <see cref="IChatMessageTokenCounter"/> when implemented, otherwise falls back to
/// counting the message's text contents plus a fixed per-message overhead.
/// </summary>
public static class ChatMessageTokenCounterExtensions
{
    /// <summary>Per-message role/format overhead applied by the text-based fallback.</summary>
    public const int MessageOverhead = 4;

    /// <summary>
    /// Counts tokens for <paramref name="message"/> using <paramref name="counter"/>.
    /// </summary>
    public static int CountMessage(this ITokenCounter counter, ChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(counter);
        ArgumentNullException.ThrowIfNull(message);

        if (counter is IChatMessageTokenCounter chatCounter)
            return chatCounter.Count(message);

        var textTokens = 0;
        foreach (var content in message.Contents)
        {
            if (content is TextContent textContent && !string.IsNullOrEmpty(textContent.Text))
                textTokens += counter.Count(textContent.Text);
        }
        return textTokens + MessageOverhead;
    }
}
