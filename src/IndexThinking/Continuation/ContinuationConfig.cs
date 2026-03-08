namespace IndexThinking.Continuation;

/// <summary>
/// Configuration for response continuation handling.
/// </summary>
/// <remarks>
/// These settings control how truncated responses are continued.
/// The actual continuation loop is implemented in ThinkingChatClient (v0.9.0).
/// </remarks>
public sealed record ContinuationConfig
{
    /// <summary>
    /// Default configuration instance.
    /// </summary>
    public static ContinuationConfig Default { get; } = new();

    /// <summary>
    /// Maximum number of continuation attempts.
    /// Default: 5
    /// </summary>
    /// <remarks>
    /// This prevents infinite loops when a model keeps hitting token limits.
    /// Set to 0 to disable automatic continuation.
    /// </remarks>
    public int MaxContinuations { get; init; } = 5;

    /// <summary>
    /// Maximum total time for all continuation attempts.
    /// Default: 5 minutes
    /// </summary>
    public TimeSpan MaxTotalDuration { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Delay between continuation attempts.
    /// Default: 0 (no delay)
    /// </summary>
    public TimeSpan DelayBetweenContinuations { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// Whether to attempt JSON recovery on truncated responses.
    /// Default: true
    /// </summary>
    public bool EnableJsonRecovery { get; init; } = true;

    /// <summary>
    /// Whether to attempt code block recovery on truncated responses.
    /// Default: true
    /// </summary>
    public bool EnableCodeBlockRecovery { get; init; } = true;

    /// <summary>
    /// The prompt template for requesting continuation.
    /// Use {previous_response} placeholder for the truncated text.
    /// Default: "Please continue from where you left off."
    /// </summary>
    public string ContinuationPrompt { get; init; } = "Continue the response directly from where it was cut off. Do not repeat any previous content.";

    /// <summary>
    /// Whether to include truncated response in continuation request.
    /// Default: true
    /// </summary>
    /// <remarks>
    /// When true, the previous truncated response is sent as an assistant message
    /// before the continuation prompt. This helps the model resume correctly.
    /// </remarks>
    public bool IncludePreviousResponse { get; init; } = true;

    /// <summary>
    /// Minimum characters that must be added per continuation to consider it progress.
    /// If a continuation adds fewer characters, it may indicate a problem.
    /// Default: 10
    /// </summary>
    public int MinProgressPerContinuation { get; init; } = 10;

    /// <summary>
    /// Maximum context tokens allowed for continuation requests.
    /// When set, the handler estimates token count before sending continuation
    /// and stops if the total would exceed this limit.
    /// Default: null (no context token validation)
    /// </summary>
    /// <remarks>
    /// Set this to the model's context window size (or slightly below) to prevent
    /// 400 Bad Request errors when continuation messages exceed the model's capacity.
    /// When null, continuation requests are sent without token budget validation.
    /// </remarks>
    public int? MaxContextTokens { get; init; }

    /// <summary>
    /// Whether to throw when max continuations is reached without completing.
    /// Default: false (returns partial response instead)
    /// </summary>
    public bool ThrowOnMaxContinuations { get; init; }

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">When configuration values are invalid.</exception>
    public void Validate()
    {
        if (MaxContinuations < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxContinuations), "Must be non-negative");
        }

        if (MaxTotalDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxTotalDuration), "Must be non-negative");
        }

        if (MinProgressPerContinuation < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MinProgressPerContinuation), "Must be non-negative");
        }

        if (MaxContextTokens is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxContextTokens), "Must be positive when set");
        }
    }
}
