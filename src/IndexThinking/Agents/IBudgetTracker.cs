using IndexThinking.Core;
using Microsoft.Extensions.AI;

namespace IndexThinking.Agents;

/// <summary>
/// Tracks token usage within a thinking turn.
/// Advisory only - does not enforce limits.
/// </summary>
/// <remarks>
/// Based on TALE research:
/// - Budget tracking is informational, not enforced
/// - LLMs may need to exceed budgets for correctness
/// - Tracking helps with observability and optimization
/// </remarks>
public interface IBudgetTracker
{
    /// <summary>
    /// Records tokens consumed from a response.
    /// </summary>
    /// <param name="response">The chat response.</param>
    /// <param name="thinking">Parsed thinking content, if any.</param>
    void RecordResponse(ChatResponse response, ThinkingContent? thinking);

    /// <summary>
    /// Sets the initial input token count.
    /// </summary>
    /// <param name="inputTokens">Number of input tokens.</param>
    void SetInputTokens(int inputTokens);

    /// <summary>
    /// Gets current usage metrics.
    /// </summary>
    /// <returns>The current budget usage.</returns>
    BudgetUsage GetUsage();

    /// <summary>
    /// Checks if thinking budget is exceeded (advisory).
    /// </summary>
    /// <param name="config">The budget configuration.</param>
    /// <returns>True if thinking tokens exceed the budget.</returns>
    bool IsThinkingBudgetExceeded(BudgetConfig config);

    /// <summary>
    /// Checks if answer budget is exceeded (advisory).
    /// </summary>
    /// <param name="config">The budget configuration.</param>
    /// <returns>True if output tokens exceed the budget.</returns>
    bool IsAnswerBudgetExceeded(BudgetConfig config);

    /// <summary>
    /// Resets the tracker for a new turn.
    /// </summary>
    void Reset();
}

/// <summary>
/// Current budget usage within a turn.
/// </summary>
public sealed record BudgetUsage
{
    /// <summary>Number of tokens in the input messages.</summary>
    public int InputTokens { get; init; }

    /// <summary>Number of tokens used for thinking/reasoning.</summary>
    public int ThinkingTokens { get; init; }

    /// <summary>Number of tokens in the output (answer) portion.</summary>
    public int OutputTokens { get; init; }

    /// <summary>Total tokens used in this turn.</summary>
    public int TotalTokens => InputTokens + ThinkingTokens + OutputTokens;

    /// <summary>Empty usage instance.</summary>
    public static BudgetUsage Empty { get; } = new();
}
