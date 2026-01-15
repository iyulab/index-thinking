using Microsoft.Extensions.Caching.Distributed;
using IndexThinking.Abstractions;
using IndexThinking.Core;

namespace IndexThinking.Stores;

/// <summary>
/// <see cref="IDistributedCache"/>-based implementation of <see cref="IThinkingStateStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// This implementation wraps any <see cref="IDistributedCache"/> backend, enabling support for
/// Redis, SQL Server, NCache, and other distributed cache providers with zero additional code.
/// </para>
/// <para>
/// Uses <see cref="ThinkingStateSerializer"/> for JSON serialization of <see cref="ThinkingState"/>.
/// </para>
/// </remarks>
public sealed class DistributedCacheThinkingStateStore : IThinkingStateStore
{
    private readonly IDistributedCache _cache;
    private readonly DistributedCacheStateStoreOptions _options;

    /// <summary>
    /// Creates a new distributed cache state store.
    /// </summary>
    /// <param name="cache">The distributed cache implementation.</param>
    /// <param name="options">Configuration options.</param>
    public DistributedCacheThinkingStateStore(
        IDistributedCache cache,
        DistributedCacheStateStoreOptions? options = null)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _options = options ?? new DistributedCacheStateStoreOptions();
    }

    /// <inheritdoc />
    public async Task<ThinkingState?> GetAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var key = GetKey(sessionId);
        var data = await _cache.GetAsync(key, cancellationToken);

        if (data is null || data.Length == 0)
        {
            return null;
        }

        return ThinkingStateSerializer.Deserialize(data);
    }

    /// <inheritdoc />
    public async Task SetAsync(string sessionId, ThinkingState state, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(state);

        var key = GetKey(sessionId);
        var data = ThinkingStateSerializer.Serialize(state);

        var cacheOptions = CreateCacheEntryOptions();
        await _cache.SetAsync(key, data, cacheOptions, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var key = GetKey(sessionId);
        await _cache.RemoveAsync(key, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var key = GetKey(sessionId);
        var data = await _cache.GetAsync(key, cancellationToken);

        return data is not null && data.Length > 0;
    }

    /// <summary>
    /// Refreshes the sliding expiration for the specified session.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public async Task RefreshAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var key = GetKey(sessionId);
        await _cache.RefreshAsync(key, cancellationToken);
    }

    /// <summary>
    /// Gets the cache key for the specified session ID.
    /// </summary>
    private string GetKey(string sessionId) => $"{_options.KeyPrefix}{sessionId}";

    /// <summary>
    /// Creates cache entry options based on configuration.
    /// </summary>
    private DistributedCacheEntryOptions CreateCacheEntryOptions()
    {
        var options = new DistributedCacheEntryOptions();

        if (_options.AbsoluteExpiration.HasValue)
        {
            options.AbsoluteExpirationRelativeToNow = _options.AbsoluteExpiration;
        }

        if (_options.SlidingExpiration.HasValue)
        {
            options.SlidingExpiration = _options.SlidingExpiration;
        }

        return options;
    }
}

/// <summary>
/// Configuration options for <see cref="DistributedCacheThinkingStateStore"/>.
/// </summary>
public sealed class DistributedCacheStateStoreOptions
{
    /// <summary>
    /// Prefix for all cache keys. Default: "thinking:state:".
    /// </summary>
    /// <remarks>
    /// Use this to namespace your cache keys and avoid collisions with other applications
    /// sharing the same distributed cache.
    /// </remarks>
    public string KeyPrefix { get; set; } = "thinking:state:";

    /// <summary>
    /// Absolute expiration time relative to now. Default: 1 hour.
    /// </summary>
    /// <remarks>
    /// State entries will be automatically removed after this duration,
    /// regardless of access patterns.
    /// </remarks>
    public TimeSpan? AbsoluteExpiration { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Sliding expiration time. Default: 15 minutes.
    /// </summary>
    /// <remarks>
    /// State entries will be removed if not accessed within this duration.
    /// Each access resets the expiration timer.
    /// </remarks>
    public TimeSpan? SlidingExpiration { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Creates default options suitable for most use cases.
    /// </summary>
    public static DistributedCacheStateStoreOptions Default => new();

    /// <summary>
    /// Creates options optimized for long-running sessions.
    /// </summary>
    /// <remarks>
    /// Uses 24-hour absolute expiration and 1-hour sliding expiration.
    /// </remarks>
    public static DistributedCacheStateStoreOptions LongRunning => new()
    {
        AbsoluteExpiration = TimeSpan.FromHours(24),
        SlidingExpiration = TimeSpan.FromHours(1)
    };

    /// <summary>
    /// Creates options for ephemeral sessions with no sliding expiration.
    /// </summary>
    /// <param name="duration">The absolute duration before expiration.</param>
    /// <returns>Configured options.</returns>
    public static DistributedCacheStateStoreOptions Ephemeral(TimeSpan duration) => new()
    {
        AbsoluteExpiration = duration,
        SlidingExpiration = null
    };
}
