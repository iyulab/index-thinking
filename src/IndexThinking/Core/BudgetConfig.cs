namespace IndexThinking.Core;

/// <summary>
/// Token budget configuration for thinking and answering.
/// </summary>
public sealed record BudgetConfig
{
    /// <summary>
    /// Maximum tokens allocated for thinking/reasoning.
    /// </summary>
    public int ThinkingBudget { get; init; } = 4096;

    /// <summary>
    /// Maximum tokens allocated for the answer/response.
    /// </summary>
    public int AnswerBudget { get; init; } = 4096;

    /// <summary>
    /// Maximum number of continuation requests allowed.
    /// </summary>
    public int MaxContinuations { get; init; } = 5;

    /// <summary>
    /// Maximum duration for the entire request (including continuations).
    /// </summary>
    public TimeSpan MaxDuration { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Minimum tokens that must be generated per continuation to avoid infinite loops.
    /// </summary>
    public int MinProgressTokens { get; init; } = 100;
}
