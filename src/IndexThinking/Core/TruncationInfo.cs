namespace IndexThinking.Core;

/// <summary>
/// Result of truncation detection.
/// </summary>
public sealed record TruncationInfo
{
    /// <summary>
    /// Whether the response was truncated.
    /// </summary>
    public required bool IsTruncated { get; init; }

    /// <summary>
    /// The reason for truncation, if any.
    /// </summary>
    public TruncationReason Reason { get; init; }

    /// <summary>
    /// Additional details about the truncation.
    /// </summary>
    public string? Details { get; init; }

    /// <summary>
    /// Creates a non-truncated result.
    /// </summary>
    public static TruncationInfo NotTruncated => new() { IsTruncated = false, Reason = TruncationReason.None };

    /// <summary>
    /// Creates a truncated result with the specified reason.
    /// </summary>
    public static TruncationInfo Truncated(TruncationReason reason, string? details = null) =>
        new() { IsTruncated = true, Reason = reason, Details = details };
}

/// <summary>
/// Reason for response truncation or early termination.
/// </summary>
public enum TruncationReason
{
    /// <summary>Response was not truncated.</summary>
    None,

    /// <summary>Truncated due to token limit (finish_reason: length/max_tokens).</summary>
    TokenLimit,

    /// <summary>Truncated with unbalanced braces/brackets.</summary>
    UnbalancedStructure,

    /// <summary>Truncated with incomplete code block.</summary>
    IncompleteCodeBlock,

    /// <summary>Truncated mid-sentence.</summary>
    MidSentence,

    /// <summary>
    /// Response blocked by content/safety filter.
    /// OpenAI: content_filter, Google: SAFETY
    /// </summary>
    ContentFiltered,

    /// <summary>
    /// Response blocked due to potential copyright/recitation.
    /// Google: RECITATION
    /// </summary>
    Recitation,

    /// <summary>
    /// Model refused to generate response due to safety concerns.
    /// Anthropic: refusal
    /// </summary>
    Refusal,

    /// <summary>
    /// Context window exceeded (input + output too large).
    /// Anthropic: model_context_window_exceeded
    /// </summary>
    ContextWindowExceeded
}
