using IndexThinking.Abstractions;

namespace IndexThinking.Memory;

/// <summary>
/// Delegate for memory recall operations.
/// </summary>
/// <remarks>
/// This delegate matches the signature of Memory-Indexer's RecallAsync,
/// allowing easy integration without direct package dependency.
/// </remarks>
public delegate Task<MemoryRecallResult> MemoryRecallDelegate(
    string userId,
    string? sessionId,
    string query,
    int limit,
    CancellationToken cancellationToken);

/// <summary>
/// Result of a memory recall delegate call.
/// </summary>
/// <remarks>
/// This record provides a simple contract for memory recall results
/// without requiring Memory-Indexer types directly.
/// </remarks>
public sealed record MemoryRecallResult
{
    /// <summary>
    /// User-scoped memories (cross-session facts).
    /// </summary>
    public IReadOnlyList<(string Content, float? Relevance)> UserMemories { get; init; } = [];

    /// <summary>
    /// Session-scoped memories (current session context).
    /// </summary>
    public IReadOnlyList<(string Content, float? Relevance)> SessionMemories { get; init; } = [];

    /// <summary>
    /// Topic-scoped memories (current topic within session).
    /// </summary>
    public IReadOnlyList<(string Content, float? Relevance)> TopicMemories { get; init; } = [];

    /// <summary>
    /// Empty result with no memories.
    /// </summary>
    public static readonly MemoryRecallResult Empty = new();
}

/// <summary>
/// A memory provider that delegates to a function.
/// </summary>
/// <remarks>
/// <para>
/// This provider enables integration with any memory backend without requiring
/// direct package dependencies. It accepts a <see cref="MemoryRecallDelegate"/>
/// that performs the actual recall operation.
/// </para>
/// <para>
/// Example usage with Memory-Indexer:
/// </para>
/// <code>
/// // In your application startup
/// var memoryService = serviceProvider.GetRequiredService&lt;IMemoryService&gt;();
/// var provider = new FuncMemoryProvider(async (userId, sessionId, query, limit, ct) =>
/// {
///     var context = await memoryService.RecallAsync(userId, sessionId, query, limit, ct);
///     return new MemoryRecallResult
///     {
///         UserMemories = context.UserMemories.Select(m => (m.Content, m.Relevance)).ToList(),
///         SessionMemories = context.SessionMemories.Select(m => (m.Content, m.Relevance)).ToList(),
///         TopicMemories = context.TopicMemories.Select(m => (m.Content, m.Relevance)).ToList()
///     };
/// });
/// </code>
/// </remarks>
public sealed class FuncMemoryProvider : IMemoryProvider
{
    private readonly MemoryRecallDelegate _recallDelegate;

    /// <summary>
    /// Creates a new <see cref="FuncMemoryProvider"/> with the specified recall delegate.
    /// </summary>
    /// <param name="recallDelegate">The delegate that performs memory recall.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="recallDelegate"/> is null.</exception>
    public FuncMemoryProvider(MemoryRecallDelegate recallDelegate)
    {
        _recallDelegate = recallDelegate ?? throw new ArgumentNullException(nameof(recallDelegate));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Always returns <c>true</c> as this provider has a configured delegate.
    /// </remarks>
    public bool IsConfigured => true;

    /// <inheritdoc />
    public async Task<MemoryRecallContext> RecallAsync(
        string userId,
        string? sessionId,
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var result = await _recallDelegate(userId, sessionId, query, limit, cancellationToken);

        var userMemories = ConvertToEntries(result.UserMemories, "user");
        var sessionMemories = ConvertToEntries(result.SessionMemories, "session");
        var topicMemories = ConvertToEntries(result.TopicMemories, "topic");

        var allMemories = userMemories
            .Concat(sessionMemories)
            .Concat(topicMemories)
            .ToList();

        return new MemoryRecallContext
        {
            Query = query,
            Memories = allMemories,
            UserMemories = userMemories,
            SessionMemories = sessionMemories,
            TopicMemories = topicMemories
        };
    }

    private static List<MemoryEntry> ConvertToEntries(
        IReadOnlyList<(string Content, float? Relevance)> memories,
        string scope)
    {
        return memories.Select(m => new MemoryEntry
        {
            Content = m.Content,
            Scope = scope,
            Relevance = m.Relevance
        }).ToList();
    }
}
