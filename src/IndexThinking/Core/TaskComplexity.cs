namespace IndexThinking.Core;

/// <summary>
/// Task complexity level for budget estimation.
/// </summary>
public enum TaskComplexity
{
    /// <summary>Quick factual answers, simple lookups.</summary>
    Simple,

    /// <summary>Code explanation, summaries, moderate analysis.</summary>
    Moderate,

    /// <summary>Bug fixing, complex analysis, multi-step reasoning.</summary>
    Complex,

    /// <summary>Multi-step deep research, comprehensive reports.</summary>
    Research
}
