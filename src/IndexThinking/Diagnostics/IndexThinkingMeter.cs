using System.Diagnostics.Metrics;
using IndexThinking.Agents;
using IndexThinking.Core;

namespace IndexThinking.Diagnostics;

/// <summary>
/// Provides IndexThinking-specific metrics using the .NET Meter API.
/// </summary>
/// <remarks>
/// <para>
/// This meter exposes metrics that complement the GenAI semantic conventions
/// provided by Microsoft.Extensions.AI's UseOpenTelemetry() method.
/// </para>
/// <para>
/// To enable these metrics, configure your OpenTelemetry provider to listen
/// to the "IndexThinking" meter:
/// <code>
/// builder.Services.AddOpenTelemetry()
///     .WithMetrics(metrics => metrics
///         .AddMeter("IndexThinking")  // Enable IndexThinking metrics
///         .AddMeter("Microsoft.Extensions.AI"));  // Enable M.E.AI metrics
/// </code>
/// </para>
/// </remarks>
public sealed class IndexThinkingMeter : IDisposable
{
    /// <summary>
    /// The meter name used for OpenTelemetry configuration.
    /// </summary>
    public const string MeterName = "IndexThinking";

    private readonly Meter _meter;
    private readonly Counter<long> _turnsTotal;
    private readonly Counter<long> _continuationsTotal;
    private readonly Counter<long> _truncationsTotal;
    private readonly Counter<long> _thinkingTokensTotal;
    private readonly Histogram<double> _turnDuration;

    /// <summary>
    /// Creates a new <see cref="IndexThinkingMeter"/> with the default meter factory.
    /// </summary>
    public IndexThinkingMeter()
        : this(null)
    {
    }

    /// <summary>
    /// Creates a new <see cref="IndexThinkingMeter"/> with the specified meter factory.
    /// </summary>
    /// <param name="meterFactory">
    /// Optional meter factory for integration with OpenTelemetry.
    /// If null, creates a standalone meter.
    /// </param>
    public IndexThinkingMeter(IMeterFactory? meterFactory)
    {
        _meter = meterFactory?.Create(MeterName) ?? new Meter(MeterName);

        _turnsTotal = _meter.CreateCounter<long>(
            name: "indexthinking.turns.total",
            unit: "{turn}",
            description: "Total number of thinking turns processed");

        _continuationsTotal = _meter.CreateCounter<long>(
            name: "indexthinking.continuations.total",
            unit: "{continuation}",
            description: "Total number of continuation requests made");

        _truncationsTotal = _meter.CreateCounter<long>(
            name: "indexthinking.truncations.total",
            unit: "{truncation}",
            description: "Total number of truncated responses (incomplete turns)");

        _thinkingTokensTotal = _meter.CreateCounter<long>(
            name: "indexthinking.tokens.thinking",
            unit: "{token}",
            description: "Total thinking/reasoning tokens consumed");

        _turnDuration = _meter.CreateHistogram<double>(
            name: "indexthinking.turn.duration",
            unit: "ms",
            description: "Duration of thinking turn processing");
    }

    /// <summary>
    /// Records metrics from a completed turn.
    /// </summary>
    /// <param name="turnResult">The completed turn result.</param>
    /// <param name="sessionId">Optional session ID for tagging.</param>
    public void RecordTurn(TurnResult turnResult, string? sessionId = null)
    {
        ArgumentNullException.ThrowIfNull(turnResult);

        var complexity = turnResult.Metrics.DetectedComplexity;
        var tags = CreateTags(complexity, sessionId);

        _turnsTotal.Add(1, tags);
        _turnDuration.Record(turnResult.Metrics.Duration.TotalMilliseconds, tags);

        if (turnResult.Metrics.ThinkingTokens > 0)
        {
            _thinkingTokensTotal.Add(turnResult.Metrics.ThinkingTokens, tags);
        }

        if (turnResult.Metrics.ContinuationCount > 0)
        {
            _continuationsTotal.Add(turnResult.Metrics.ContinuationCount, tags);
        }

        if (turnResult.WasTruncated)
        {
            _truncationsTotal.Add(1, tags);
        }
    }

    /// <summary>
    /// Records a single turn completion.
    /// </summary>
    /// <param name="metrics">Turn metrics.</param>
    /// <param name="wasTruncated">Whether the turn was truncated.</param>
    /// <param name="complexity">Detected task complexity.</param>
    /// <param name="sessionId">Optional session ID.</param>
    public void RecordTurn(
        TurnMetrics metrics,
        bool wasTruncated = false,
        TaskComplexity? complexity = null,
        string? sessionId = null)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        var tags = CreateTags(complexity, sessionId);

        _turnsTotal.Add(1, tags);
        _turnDuration.Record(metrics.Duration.TotalMilliseconds, tags);

        if (metrics.ThinkingTokens > 0)
        {
            _thinkingTokensTotal.Add(metrics.ThinkingTokens, tags);
        }

        if (metrics.ContinuationCount > 0)
        {
            _continuationsTotal.Add(metrics.ContinuationCount, tags);
        }

        if (wasTruncated)
        {
            _truncationsTotal.Add(1, tags);
        }
    }

    private static KeyValuePair<string, object?>[] CreateTags(TaskComplexity? complexity, string? sessionId)
    {
        var tags = new List<KeyValuePair<string, object?>>();

        if (complexity.HasValue)
        {
            tags.Add(new KeyValuePair<string, object?>("indexthinking.complexity", complexity.Value.ToString().ToLowerInvariant()));
        }

        if (!string.IsNullOrEmpty(sessionId))
        {
            tags.Add(new KeyValuePair<string, object?>("indexthinking.session_id", sessionId));
        }

        return tags.ToArray();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _meter.Dispose();
    }
}
