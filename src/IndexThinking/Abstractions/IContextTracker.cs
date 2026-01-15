using IndexThinking.Context;
using Microsoft.Extensions.AI;

namespace IndexThinking.Abstractions;

/// <summary>
/// Tracks conversation context within sessions.
/// </summary>
/// <remarks>
/// <para>
/// The context tracker maintains a sliding window of recent conversation turns
/// for each session. This enables context-aware processing where the LLM can
/// understand references to previous messages (e.g., "Do that thing again").
/// </para>
/// <para>
/// Unlike approaches that try to resolve pronouns and references programmatically,
/// IndexThinking provides the conversation context to the LLM, letting it naturally
/// understand contextual references. This is more robust and requires no extra LLM calls.
/// </para>
/// <para>
/// The default implementation (<see cref="Context.InMemoryContextTracker"/>) uses
/// in-memory storage with configurable window size and TTL.
/// </para>
/// </remarks>
public interface IContextTracker
{
    /// <summary>
    /// Records a conversation turn (user message + optional assistant response).
    /// </summary>
    /// <param name="sessionId">Session identifier.</param>
    /// <param name="userMessage">The user's message.</param>
    /// <param name="response">The assistant's response, if available.</param>
    /// <remarks>
    /// Call this after each turn to maintain conversation history.
    /// The tracker automatically manages the sliding window, evicting
    /// older turns when the limit is reached.
    /// </remarks>
    void Track(string sessionId, ChatMessage userMessage, ChatResponse? response = null);

    /// <summary>
    /// Gets the conversation context for a session.
    /// </summary>
    /// <param name="sessionId">Session identifier.</param>
    /// <returns>
    /// The conversation context containing recent turns.
    /// Returns an empty context if no history exists.
    /// </returns>
    ConversationContext GetContext(string sessionId);

    /// <summary>
    /// Clears all conversation history for a session.
    /// </summary>
    /// <param name="sessionId">Session identifier.</param>
    /// <remarks>
    /// Use this when starting a new conversation topic or when the user
    /// explicitly requests a context reset.
    /// </remarks>
    void Clear(string sessionId);

    /// <summary>
    /// Checks if a session has any conversation history.
    /// </summary>
    /// <param name="sessionId">Session identifier.</param>
    /// <returns>True if the session has at least one recorded turn.</returns>
    bool HasContext(string sessionId);
}
