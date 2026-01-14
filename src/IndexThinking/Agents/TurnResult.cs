using IndexThinking.Core;
using Microsoft.Extensions.AI;

namespace IndexThinking.Agents;

/// <summary>
/// Result of a completed thinking turn.
/// </summary>
public sealed record TurnResult
{
    /// <summary>
    /// The final (possibly combined) response.
    /// </summary>
    public required ChatResponse Response { get; init; }

    /// <summary>
    /// Parsed thinking content, if any.
    /// </summary>
    public ThinkingContent? ThinkingContent { get; init; }

    /// <summary>
    /// Provider-specific reasoning state for multi-turn preservation.
    /// </summary>
    public ReasoningState? ReasoningState { get; init; }

    /// <summary>
    /// Metrics collected during this turn.
    /// </summary>
    public required TurnMetrics Metrics { get; init; }

    /// <summary>
    /// Whether continuation was needed to complete the response.
    /// </summary>
    public bool WasContinued => Metrics.ContinuationCount > 0;

    /// <summary>
    /// Whether the response was truncated (max continuations reached without completion).
    /// </summary>
    public bool WasTruncated { get; init; }

    /// <summary>
    /// The turn context that produced this result.
    /// </summary>
    public ThinkingContext? Context { get; init; }

    /// <summary>
    /// Creates a successful result without continuation.
    /// </summary>
    public static TurnResult Success(
        ChatResponse response,
        TurnMetrics metrics,
        ThinkingContent? thinkingContent = null,
        ReasoningState? reasoningState = null)
    {
        return new TurnResult
        {
            Response = response,
            Metrics = metrics,
            ThinkingContent = thinkingContent,
            ReasoningState = reasoningState,
            WasTruncated = false
        };
    }

    /// <summary>
    /// Creates a result that was truncated (incomplete).
    /// </summary>
    public static TurnResult Truncated(
        ChatResponse partialResponse,
        TurnMetrics metrics,
        ThinkingContent? thinkingContent = null,
        ReasoningState? reasoningState = null)
    {
        return new TurnResult
        {
            Response = partialResponse,
            Metrics = metrics,
            ThinkingContent = thinkingContent,
            ReasoningState = reasoningState,
            WasTruncated = true
        };
    }

    /// <summary>
    /// Gets the final response text.
    /// </summary>
    public string? ResponseText => Response.Text;

    /// <summary>
    /// Gets whether thinking content was extracted.
    /// </summary>
    public bool HasThinkingContent => ThinkingContent is not null;

    /// <summary>
    /// Gets whether reasoning state is available for multi-turn.
    /// </summary>
    public bool HasReasoningState => ReasoningState is not null;
}
