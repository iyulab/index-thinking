using System.Collections.Concurrent;
using IndexThinking.Abstractions;
using IndexThinking.Core;

namespace IndexThinking.Stores;

/// <summary>
/// In-memory implementation of <see cref="IThinkingStateStore"/>.
/// Suitable for single-instance scenarios and testing.
/// </summary>
public sealed class InMemoryThinkingStateStore : IThinkingStateStore
{
    private readonly ConcurrentDictionary<string, ThinkingState> _states = new();

    /// <inheritdoc />
    public Task<ThinkingState?> GetAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        _states.TryGetValue(sessionId, out var state);
        return Task.FromResult(state);
    }

    /// <inheritdoc />
    public Task SetAsync(string sessionId, ThinkingState state, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(state);

        _states[sessionId] = state;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        _states.TryRemove(sessionId, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        return Task.FromResult(_states.ContainsKey(sessionId));
    }

    /// <summary>
    /// Gets the current count of stored states. Useful for testing.
    /// </summary>
    public int Count => _states.Count;

    /// <summary>
    /// Clears all stored states. Useful for testing.
    /// </summary>
    public void Clear() => _states.Clear();
}
