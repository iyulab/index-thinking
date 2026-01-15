namespace IndexThinking.Context;

/// <summary>
/// Configuration options for context tracking.
/// </summary>
public sealed record ContextTrackerOptions
{
    /// <summary>
    /// Maximum number of turns to keep in the sliding window.
    /// Default: 10 turns.
    /// </summary>
    /// <remarks>
    /// Older turns are automatically evicted when this limit is reached.
    /// A reasonable value depends on the typical conversation length
    /// and the LLM's context window size.
    /// </remarks>
    public int MaxTurns { get; set; } = 10;

    /// <summary>
    /// Time-to-live for session contexts.
    /// Sessions without activity for this duration are automatically cleared.
    /// Default: 1 hour.
    /// </summary>
    public TimeSpan SessionTtl { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Whether to automatically start a cleanup timer for expired sessions.
    /// Default: true.
    /// </summary>
    public bool EnableCleanupTimer { get; set; } = true;

    /// <summary>
    /// Interval for the cleanup timer.
    /// Default: 5 minutes.
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Default options with reasonable defaults for most use cases.
    /// </summary>
    public static ContextTrackerOptions Default { get; } = new();
}
