using IndexThinking.Memory;

namespace IndexThinking.Abstractions;

/// <summary>
/// Provides memory recall capabilities for thinking operations.
/// </summary>
/// <remarks>
/// <para>
/// This interface abstracts memory retrieval operations, allowing IndexThinking
/// to work with various memory backends including:
/// </para>
/// <list type="bullet">
///   <item>Memory-Indexer (via <see cref="Memory.FuncMemoryProvider"/>)</item>
///   <item>Custom memory implementations</item>
///   <item>No-op implementation for zero-config usage (<see cref="Memory.NullMemoryProvider"/>)</item>
/// </list>
/// <para>
/// IndexThinking uses IMemoryProvider to enrich thinking context with relevant
/// memories before processing a request. The integration is optional - when
/// <see cref="NullMemoryProvider"/> is used (default), memory features are disabled
/// and IndexThinking works without any memory backend.
/// </para>
/// </remarks>
public interface IMemoryProvider
{
    /// <summary>
    /// Indicates whether this provider is configured with a memory backend.
    /// </summary>
    /// <remarks>
    /// Returns <c>false</c> for <see cref="Memory.NullMemoryProvider"/> (no-op),
    /// <c>true</c> for actual memory implementations.
    /// Use this to conditionally enable memory-dependent features.
    /// </remarks>
    bool IsConfigured { get; }

    /// <summary>
    /// Recalls relevant memories for a given query.
    /// </summary>
    /// <param name="userId">User identifier for user-scoped memories.</param>
    /// <param name="sessionId">Optional session identifier for session-scoped memories.</param>
    /// <param name="query">Query text for semantic search.</param>
    /// <param name="limit">Maximum number of memories to return (default: 10).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A <see cref="MemoryRecallContext"/> containing recalled memories grouped by scope.
    /// Returns an empty context if no memories are found or provider is not configured.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Memory scopes:
    /// </para>
    /// <list type="bullet">
    ///   <item><b>User</b>: Cross-session facts about the user (e.g., preferences, known facts)</item>
    ///   <item><b>Session</b>: Current session context (e.g., recent conversation)</item>
    ///   <item><b>Topic</b>: Current topic within session (e.g., current code file being discussed)</item>
    /// </list>
    /// <para>
    /// If <paramref name="sessionId"/> is null, only user-scoped memories are returned.
    /// If provided, session and topic memories are included based on relevance.
    /// </para>
    /// </remarks>
    Task<MemoryRecallContext> RecallAsync(
        string userId,
        string? sessionId,
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores memories to the long-term memory backend.
    /// </summary>
    /// <param name="userId">User identifier for user-scoped memories.</param>
    /// <param name="sessionId">Optional session identifier for session-scoped memories.</param>
    /// <param name="memories">The memories to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// Use this method to persist important facts extracted during conversation:
    /// </para>
    /// <list type="bullet">
    ///   <item>User preferences and settings</item>
    ///   <item>Important context from the current session</item>
    ///   <item>Key decisions or facts mentioned by the user</item>
    /// </list>
    /// <para>
    /// The memory backend determines how memories are stored and indexed.
    /// For <see cref="Memory.NullMemoryProvider"/>, this is a no-op.
    /// </para>
    /// </remarks>
    Task RememberAsync(
        string userId,
        string? sessionId,
        IEnumerable<MemoryStoreRequest> memories,
        CancellationToken cancellationToken = default);
}
