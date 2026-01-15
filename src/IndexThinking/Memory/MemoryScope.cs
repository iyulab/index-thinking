namespace IndexThinking.Memory;

/// <summary>
/// Defines the scope of a memory entry.
/// </summary>
/// <remarks>
/// <para>
/// Memory scopes determine the visibility and persistence of memories:
/// </para>
/// <list type="bullet">
///   <item><see cref="User"/>: Cross-session facts about the user</item>
///   <item><see cref="Session"/>: Current session context</item>
///   <item><see cref="Topic"/>: Current topic within session</item>
/// </list>
/// </remarks>
public enum MemoryScope
{
    /// <summary>
    /// User-scoped memory that persists across all sessions.
    /// </summary>
    /// <remarks>
    /// Examples: user preferences, known facts about the user, long-term context.
    /// </remarks>
    User = 0,

    /// <summary>
    /// Session-scoped memory that persists for the current session only.
    /// </summary>
    /// <remarks>
    /// Examples: recent conversation context, session-specific decisions.
    /// </remarks>
    Session = 1,

    /// <summary>
    /// Topic-scoped memory for the current topic within a session.
    /// </summary>
    /// <remarks>
    /// Examples: current code file being discussed, current problem context.
    /// </remarks>
    Topic = 2
}
