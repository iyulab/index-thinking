using IndexThinking.Continuation;
using IndexThinking.Core;

namespace IndexThinking.Agents;

/// <summary>
/// Configuration options for IndexThinking turn management services.
/// </summary>
public class AgentOptions
{
    /// <summary>
    /// Default budget configuration used when no specific budget is provided.
    /// </summary>
    public BudgetConfig DefaultBudget { get; set; } = new BudgetConfig();

    /// <summary>
    /// Default continuation configuration for handling truncated responses.
    /// </summary>
    public ContinuationConfig DefaultContinuation { get; set; } = ContinuationConfig.Default;

    /// <summary>
    /// Whether to automatically estimate task complexity when not explicitly provided.
    /// </summary>
    public bool AutoEstimateComplexity { get; set; } = true;
}
