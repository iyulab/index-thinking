using Microsoft.Extensions.AI;

namespace IndexThinking.Context;

/// <summary>
/// Represents a single conversation turn (user message + optional assistant response).
/// </summary>
public sealed record ConversationTurn
{
    /// <summary>
    /// The user's message in this turn.
    /// </summary>
    public required ChatMessage UserMessage { get; init; }

    /// <summary>
    /// The assistant's response to the user message, if available.
    /// </summary>
    public ChatResponse? AssistantResponse { get; init; }

    /// <summary>
    /// When this turn occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Unique identifier for this turn.
    /// </summary>
    public string TurnId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets the assistant's text response, if available.
    /// </summary>
    public string? AssistantText => AssistantResponse?.Messages?.FirstOrDefault()?.Text;

    /// <summary>
    /// Gets the user's text message.
    /// </summary>
    public string? UserText => UserMessage.Text;
}

/// <summary>
/// Represents the conversation context for a session.
/// Contains recent conversation history for context-aware processing.
/// </summary>
public sealed record ConversationContext
{
    /// <summary>
    /// Recent conversation turns, ordered from oldest to newest.
    /// </summary>
    public IReadOnlyList<ConversationTurn> RecentTurns { get; init; } = [];

    /// <summary>
    /// Total number of turns tracked in this session (may exceed RecentTurns.Count).
    /// </summary>
    public int TotalTurnCount { get; init; }

    /// <summary>
    /// Session identifier this context belongs to.
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// When the first turn in this session occurred.
    /// </summary>
    public DateTimeOffset? SessionStartedAt { get; init; }

    /// <summary>
    /// When the most recent turn occurred.
    /// </summary>
    public DateTimeOffset? LastTurnAt { get; init; }

    /// <summary>
    /// Whether this context has any conversation history.
    /// </summary>
    public bool HasHistory => RecentTurns.Count > 0;

    /// <summary>
    /// Number of turns in the current context window.
    /// </summary>
    public int WindowSize => RecentTurns.Count;

    /// <summary>
    /// Creates an empty context for a new session.
    /// </summary>
    public static ConversationContext Empty(string sessionId) => new()
    {
        SessionId = sessionId,
        RecentTurns = [],
        TotalTurnCount = 0
    };
}
