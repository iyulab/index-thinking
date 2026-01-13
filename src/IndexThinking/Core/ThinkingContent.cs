namespace IndexThinking.Core;

/// <summary>
/// Parsed thinking/reasoning content from an LLM response.
/// </summary>
public sealed record ThinkingContent
{
    /// <summary>
    /// The thinking/reasoning text (may be summarized).
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Number of tokens used for thinking.
    /// </summary>
    public int TokenCount { get; init; }

    /// <summary>
    /// Whether this is a summary or full thinking trace.
    /// </summary>
    public bool IsSummarized { get; init; }
}
