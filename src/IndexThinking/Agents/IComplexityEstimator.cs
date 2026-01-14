using IndexThinking.Core;
using Microsoft.Extensions.AI;

namespace IndexThinking.Agents;

/// <summary>
/// Estimates task complexity from input messages.
/// Used to recommend appropriate token budgets (advisory, not enforced).
/// </summary>
/// <remarks>
/// Design based on TALE (Token-Budget-Aware LLM Reasoning) research:
/// - Complexity estimation enables smarter budget allocation
/// - Budget recommendations are advisory, not enforced
/// - Simple tasks shouldn't waste tokens on excessive reasoning
/// </remarks>
public interface IComplexityEstimator
{
    /// <summary>
    /// Estimates the complexity of a task based on input messages.
    /// </summary>
    /// <param name="messages">The conversation messages to analyze.</param>
    /// <returns>The estimated task complexity.</returns>
    TaskComplexity Estimate(IReadOnlyList<ChatMessage> messages);

    /// <summary>
    /// Gets the recommended budget for a given complexity level.
    /// </summary>
    /// <param name="complexity">The estimated complexity.</param>
    /// <returns>Recommended budget configuration (advisory).</returns>
    BudgetConfig GetRecommendedBudget(TaskComplexity complexity);
}
