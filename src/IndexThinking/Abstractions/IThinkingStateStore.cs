using IndexThinking.Core;

namespace IndexThinking.Abstractions;

/// <summary>
/// Stores thinking state for multi-turn conversations and continuation.
/// State is ephemeral (within a turn) but may need persistence for recovery.
/// </summary>
public interface IThinkingStateStore
{
    /// <summary>
    /// Gets the thinking state for the specified session.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The thinking state if found; otherwise, null.</returns>
    Task<ThinkingState?> GetAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the thinking state for the specified session.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="state">The thinking state to store.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task SetAsync(string sessionId, ThinkingState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the thinking state for the specified session.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task RemoveAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a thinking state exists for the specified session.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>True if the state exists; otherwise, false.</returns>
    Task<bool> ExistsAsync(string sessionId, CancellationToken cancellationToken = default);
}
