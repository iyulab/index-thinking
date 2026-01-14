using Microsoft.Extensions.AI;

namespace IndexThinking.Agents;

/// <summary>
/// Handles response continuation when truncation is detected.
/// </summary>
/// <remarks>
/// Implements state machine pattern:
/// 1. Check if response is truncated
/// 2. If not truncated, return immediately
/// 3. If truncated, apply recovery
/// 4. Build continuation request
/// 5. Loop until complete or max continuations reached
/// 6. Combine fragments into final response
/// </remarks>
public interface IContinuationHandler
{
    /// <summary>
    /// Processes a response and continues if truncated.
    /// </summary>
    /// <param name="context">The thinking context.</param>
    /// <param name="initialResponse">The initial (possibly truncated) response.</param>
    /// <param name="sendRequest">Function to send continuation requests.</param>
    /// <returns>The final combined response with continuation information.</returns>
    Task<ContinuationResult> HandleAsync(
        ThinkingContext context,
        ChatResponse initialResponse,
        Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>> sendRequest);
}

/// <summary>
/// Result of continuation handling.
/// </summary>
public sealed record ContinuationResult
{
    /// <summary>The final (possibly combined) response.</summary>
    public required ChatResponse FinalResponse { get; init; }

    /// <summary>Number of continuation requests made.</summary>
    public required int ContinuationCount { get; init; }

    /// <summary>Whether max continuations was reached without completion.</summary>
    public required bool ReachedMaxContinuations { get; init; }

    /// <summary>Intermediate responses from continuations.</summary>
    public IReadOnlyList<ChatResponse> IntermediateResponses { get; init; } = [];

    /// <summary>Creates a result for a non-truncated response.</summary>
    public static ContinuationResult NotTruncated(ChatResponse response) => new()
    {
        FinalResponse = response,
        ContinuationCount = 0,
        ReachedMaxContinuations = false
    };
}
