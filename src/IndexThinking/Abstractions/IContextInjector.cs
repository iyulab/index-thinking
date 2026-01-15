using IndexThinking.Context;
using Microsoft.Extensions.AI;

namespace IndexThinking.Abstractions;

/// <summary>
/// Injects conversation context into messages for context-aware LLM processing.
/// </summary>
/// <remarks>
/// <para>
/// The context injector prepares messages with conversation history so the LLM
/// can understand contextual references (e.g., "Do that thing again", "make it faster").
/// </para>
/// <para>
/// Rather than programmatically resolving pronouns and references, we provide the
/// conversation context directly to the LLM. This approach is:
/// </para>
/// <list type="bullet">
///   <item>More robust: LLMs are excellent at understanding context</item>
///   <item>Zero-cost: No additional LLM calls for query interpretation</item>
///   <item>Language-agnostic: Works with any language naturally</item>
/// </list>
/// <para>
/// The default implementation (<see cref="Context.DefaultContextInjector"/>) adds
/// previous conversation turns as additional messages.
/// </para>
/// </remarks>
public interface IContextInjector
{
    /// <summary>
    /// Injects conversation context into the message list.
    /// </summary>
    /// <param name="messages">Original messages from the user.</param>
    /// <param name="context">Conversation context to inject.</param>
    /// <returns>
    /// A new message list with context injected.
    /// The original messages are preserved at the end.
    /// </returns>
    /// <remarks>
    /// The returned messages typically include:
    /// <list type="number">
    ///   <item>Previous user messages and assistant responses</item>
    ///   <item>The current user message(s)</item>
    /// </list>
    /// </remarks>
    IList<ChatMessage> InjectContext(
        IList<ChatMessage> messages,
        ConversationContext context);
}
