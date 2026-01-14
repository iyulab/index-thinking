using Microsoft.Extensions.AI;

namespace IndexThinking.Agents;

/// <summary>
/// Coordinates a single thinking turn from request to result.
/// This is the main entry point for turn-level processing.
/// </summary>
/// <remarks>
/// A turn manager orchestrates:
/// - Complexity estimation
/// - Initial request sending
/// - Continuation handling
/// - Reasoning parsing
/// - Budget tracking
/// - State storage
/// </remarks>
public interface IThinkingTurnManager
{
    /// <summary>
    /// Processes a complete thinking turn.
    /// </summary>
    /// <param name="context">The thinking context.</param>
    /// <param name="sendRequest">Function to send requests to the LLM.</param>
    /// <returns>The turn result with response and metrics.</returns>
    Task<TurnResult> ProcessTurnAsync(
        ThinkingContext context,
        Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>> sendRequest);
}
