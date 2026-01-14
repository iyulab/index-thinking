using IndexThinking.Core;

namespace IndexThinking.Agents;

/// <summary>
/// Metrics collected during a thinking turn.
/// </summary>
public sealed record TurnMetrics
{
    /// <summary>
    /// Number of tokens in the input messages.
    /// </summary>
    public int InputTokens { get; init; }

    /// <summary>
    /// Number of tokens used for thinking/reasoning.
    /// </summary>
    public int ThinkingTokens { get; init; }

    /// <summary>
    /// Number of tokens in the output (answer) portion.
    /// </summary>
    public int OutputTokens { get; init; }

    /// <summary>
    /// Total tokens used in this turn.
    /// </summary>
    public int TotalTokens => InputTokens + ThinkingTokens + OutputTokens;

    /// <summary>
    /// Number of continuation requests made.
    /// </summary>
    public int ContinuationCount { get; init; }

    /// <summary>
    /// Total duration of the turn processing.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Detected task complexity (may differ from estimated).
    /// </summary>
    public TaskComplexity? DetectedComplexity { get; init; }

    /// <summary>
    /// Whether the turn required continuation.
    /// </summary>
    public bool RequiredContinuation => ContinuationCount > 0;

    /// <summary>
    /// Average tokens per continuation (excluding initial request).
    /// </summary>
    public double AverageTokensPerContinuation =>
        ContinuationCount > 0 ? (double)OutputTokens / (ContinuationCount + 1) : OutputTokens;

    /// <summary>
    /// Empty metrics instance.
    /// </summary>
    public static TurnMetrics Empty { get; } = new();

    /// <summary>
    /// Creates a builder for constructing metrics incrementally.
    /// </summary>
    public static TurnMetricsBuilder CreateBuilder() => new();
}

/// <summary>
/// Builder for constructing <see cref="TurnMetrics"/> incrementally.
/// </summary>
public sealed class TurnMetricsBuilder
{
    private int _inputTokens;
    private int _thinkingTokens;
    private int _outputTokens;
    private int _continuationCount;
    private TimeSpan _duration;
    private TaskComplexity? _detectedComplexity;
    private readonly DateTimeOffset _startTime = DateTimeOffset.UtcNow;

    /// <summary>
    /// Sets the input token count.
    /// </summary>
    public TurnMetricsBuilder WithInputTokens(int tokens)
    {
        _inputTokens = tokens;
        return this;
    }

    /// <summary>
    /// Adds to the thinking token count.
    /// </summary>
    public TurnMetricsBuilder AddThinkingTokens(int tokens)
    {
        _thinkingTokens += tokens;
        return this;
    }

    /// <summary>
    /// Adds to the output token count.
    /// </summary>
    public TurnMetricsBuilder AddOutputTokens(int tokens)
    {
        _outputTokens += tokens;
        return this;
    }

    /// <summary>
    /// Increments the continuation count.
    /// </summary>
    public TurnMetricsBuilder IncrementContinuation()
    {
        _continuationCount++;
        return this;
    }

    /// <summary>
    /// Sets the detected complexity.
    /// </summary>
    public TurnMetricsBuilder WithComplexity(TaskComplexity complexity)
    {
        _detectedComplexity = complexity;
        return this;
    }

    /// <summary>
    /// Sets the duration explicitly.
    /// </summary>
    public TurnMetricsBuilder WithDuration(TimeSpan duration)
    {
        _duration = duration;
        return this;
    }

    /// <summary>
    /// Builds the metrics, calculating duration from start time if not set.
    /// </summary>
    public TurnMetrics Build()
    {
        var duration = _duration != TimeSpan.Zero
            ? _duration
            : DateTimeOffset.UtcNow - _startTime;

        return new TurnMetrics
        {
            InputTokens = _inputTokens,
            ThinkingTokens = _thinkingTokens,
            OutputTokens = _outputTokens,
            ContinuationCount = _continuationCount,
            Duration = duration,
            DetectedComplexity = _detectedComplexity
        };
    }
}
