using IndexThinking.Abstractions;

namespace IndexThinking.Memory;

/// <summary>
/// A no-op memory provider that returns empty results.
/// </summary>
/// <remarks>
/// <para>
/// This is the default implementation used when no memory backend is configured.
/// It enables IndexThinking to work in zero-config mode without requiring
/// Memory-Indexer or any other memory system.
/// </para>
/// <para>
/// Using <see cref="NullMemoryProvider"/> means:
/// </para>
/// <list type="bullet">
///   <item>Memory-dependent features are disabled</item>
///   <item>Context enrichment from memory is skipped</item>
///   <item>All recall operations return empty results</item>
/// </list>
/// <para>
/// This follows the null object pattern to avoid null checks throughout the codebase.
/// </para>
/// </remarks>
public sealed class NullMemoryProvider : IMemoryProvider
{
    /// <summary>
    /// Singleton instance of the null provider.
    /// </summary>
    public static readonly NullMemoryProvider Instance = new();

    /// <summary>
    /// Private constructor to enforce singleton pattern.
    /// </summary>
    private NullMemoryProvider() { }

    /// <inheritdoc />
    /// <remarks>
    /// Always returns <c>false</c> as this provider has no memory backend.
    /// </remarks>
    public bool IsConfigured => false;

    /// <inheritdoc />
    /// <remarks>
    /// Always returns an empty <see cref="MemoryRecallContext"/>.
    /// </remarks>
    public Task<MemoryRecallContext> RecallAsync(
        string userId,
        string? sessionId,
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        return Task.FromResult(MemoryRecallContext.Empty(query));
    }
}
