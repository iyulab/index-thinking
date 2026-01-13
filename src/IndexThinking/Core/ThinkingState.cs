namespace IndexThinking.Core;

/// <summary>
/// Complete thinking state for a session.
/// Tracks accumulated tokens, continuation count, and provider-specific state.
/// </summary>
public sealed record ThinkingState
{
    /// <summary>
    /// Unique session identifier.
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Model identifier used for this session.
    /// </summary>
    public string? ModelId { get; init; }

    /// <summary>
    /// Provider-specific reasoning state (encrypted content, signatures, etc.).
    /// </summary>
    public ReasoningState? ReasoningState { get; init; }

    /// <summary>
    /// Total tokens used for thinking/reasoning in this session.
    /// </summary>
    public int TotalThinkingTokens { get; init; }

    /// <summary>
    /// Total output tokens generated in this session.
    /// </summary>
    public int TotalOutputTokens { get; init; }

    /// <summary>
    /// Number of continuation requests made.
    /// </summary>
    public int ContinuationCount { get; init; }

    /// <summary>
    /// When this state was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When this state was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}
