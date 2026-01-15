using System.Collections.Concurrent;
using IndexThinking.Abstractions;
using IndexThinking.Core;

namespace IndexThinking.Stores;

/// <summary>
/// In-memory implementation of <see cref="IThinkingStateStore"/> with optional TTL support.
/// Suitable for single-instance scenarios and testing.
/// </summary>
/// <remarks>
/// When TTL is configured, expired entries are removed lazily during access operations
/// or eagerly via <see cref="CleanupExpired"/>.
/// </remarks>
public sealed class InMemoryThinkingStateStore : IThinkingStateStore, IDisposable
{
    private readonly ConcurrentDictionary<string, CacheEntry> _entries = new();
    private readonly InMemoryStateStoreOptions _options;
    private readonly Timer? _cleanupTimer;
    private bool _disposed;

    /// <summary>
    /// Creates a new in-memory state store with default options (no TTL).
    /// </summary>
    public InMemoryThinkingStateStore() : this(new InMemoryStateStoreOptions())
    {
    }

    /// <summary>
    /// Creates a new in-memory state store with the specified options.
    /// </summary>
    /// <param name="options">Configuration options.</param>
    public InMemoryThinkingStateStore(InMemoryStateStoreOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (_options.CleanupInterval.HasValue && _options.DefaultTtl.HasValue)
        {
            _cleanupTimer = new Timer(
                _ => CleanupExpired(),
                null,
                _options.CleanupInterval.Value,
                _options.CleanupInterval.Value);
        }
    }

    /// <inheritdoc />
    public Task<ThinkingState?> GetAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_entries.TryGetValue(sessionId, out var entry))
        {
            return Task.FromResult<ThinkingState?>(null);
        }

        // Check expiration
        if (entry.ExpiresAt.HasValue && entry.ExpiresAt.Value <= DateTimeOffset.UtcNow)
        {
            _entries.TryRemove(sessionId, out _);
            return Task.FromResult<ThinkingState?>(null);
        }

        // Sliding expiration: update expiry time on access
        if (_options.UseSlidingExpiration && _options.DefaultTtl.HasValue)
        {
            var newEntry = entry with { ExpiresAt = DateTimeOffset.UtcNow + _options.DefaultTtl.Value };
            _entries.TryUpdate(sessionId, newEntry, entry);
        }

        return Task.FromResult<ThinkingState?>(entry.State);
    }

    /// <inheritdoc />
    public Task SetAsync(string sessionId, ThinkingState state, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(state);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var expiresAt = _options.DefaultTtl.HasValue
            ? DateTimeOffset.UtcNow + _options.DefaultTtl.Value
            : (DateTimeOffset?)null;

        var entry = new CacheEntry(state, expiresAt);
        _entries[sessionId] = entry;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ObjectDisposedException.ThrowIf(_disposed, this);

        _entries.TryRemove(sessionId, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_entries.TryGetValue(sessionId, out var entry))
        {
            return Task.FromResult(false);
        }

        // Check expiration
        if (entry.ExpiresAt.HasValue && entry.ExpiresAt.Value <= DateTimeOffset.UtcNow)
        {
            _entries.TryRemove(sessionId, out _);
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    /// <summary>
    /// Gets the current count of stored entries (including potentially expired ones).
    /// </summary>
    public int Count => _entries.Count;

    /// <summary>
    /// Clears all stored states.
    /// </summary>
    public void Clear() => _entries.Clear();

    /// <summary>
    /// Removes all expired entries from the cache.
    /// </summary>
    /// <returns>The number of entries removed.</returns>
    public int CleanupExpired()
    {
        var now = DateTimeOffset.UtcNow;
        var removed = 0;

        foreach (var kvp in _entries)
        {
            if (kvp.Value.ExpiresAt.HasValue && kvp.Value.ExpiresAt.Value <= now)
            {
                if (_entries.TryRemove(kvp.Key, out _))
                {
                    removed++;
                }
            }
        }

        return removed;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cleanupTimer?.Dispose();
        _entries.Clear();
    }

    private sealed record CacheEntry(ThinkingState State, DateTimeOffset? ExpiresAt);
}

/// <summary>
/// Configuration options for <see cref="InMemoryThinkingStateStore"/>.
/// </summary>
public sealed record InMemoryStateStoreOptions
{
    /// <summary>
    /// Default time-to-live for cached entries.
    /// If null, entries never expire.
    /// </summary>
    public TimeSpan? DefaultTtl { get; set; }

    /// <summary>
    /// Whether to use sliding expiration (reset TTL on access).
    /// Only applies when <see cref="DefaultTtl"/> is set.
    /// Default: false (absolute expiration).
    /// </summary>
    public bool UseSlidingExpiration { get; set; }

    /// <summary>
    /// Interval for automatic cleanup of expired entries.
    /// If null, cleanup only happens lazily during access.
    /// </summary>
    public TimeSpan? CleanupInterval { get; set; }

    /// <summary>
    /// Creates options with no expiration (entries persist indefinitely).
    /// </summary>
    public static InMemoryStateStoreOptions NoExpiration => new();

    /// <summary>
    /// Creates options with the specified TTL and optional cleanup.
    /// </summary>
    /// <param name="ttl">Time-to-live for entries.</param>
    /// <param name="cleanupInterval">Optional cleanup interval.</param>
    /// <returns>Configured options.</returns>
    public static InMemoryStateStoreOptions WithTtl(TimeSpan ttl, TimeSpan? cleanupInterval = null) => new()
    {
        DefaultTtl = ttl,
        CleanupInterval = cleanupInterval
    };
}
