using IndexThinking.Abstractions;
using IndexThinking.Core;
using Microsoft.Extensions.AI;

namespace IndexThinking.Agents;

/// <summary>
/// Default implementation of <see cref="IBudgetTracker"/>.
/// </summary>
/// <remarks>
/// Token counting strategy:
/// 1. Use response.Usage if available (most accurate)
/// 2. Fall back to ITokenCounter estimation
///
/// Thread-safe for concurrent recording.
/// </remarks>
public sealed class DefaultBudgetTracker : IBudgetTracker
{
    private readonly ITokenCounter _tokenCounter;
    private int _inputTokens;
    private int _thinkingTokens;
    private int _outputTokens;
    private readonly object _lock = new();

    /// <summary>
    /// Creates a new budget tracker.
    /// </summary>
    /// <param name="tokenCounter">Token counter for estimation fallback.</param>
    public DefaultBudgetTracker(ITokenCounter tokenCounter)
    {
        _tokenCounter = tokenCounter ?? throw new ArgumentNullException(nameof(tokenCounter));
    }

    /// <inheritdoc />
    public void RecordResponse(ChatResponse response, ThinkingContent? thinking)
    {
        ArgumentNullException.ThrowIfNull(response);

        var usage = response.Usage;
        int outputTokens;
        int thinkingTokens = 0;

        if (usage is not null)
        {
            // Use actual usage from response
            outputTokens = (int)(usage.OutputTokenCount ?? EstimateOutputTokens(response));

            // Try to get thinking tokens from AdditionalCounts (OpenAI reasoning_tokens)
            if (usage.AdditionalCounts?.TryGetValue("reasoning_tokens", out var reasoningTokens) == true)
            {
                thinkingTokens = Convert.ToInt32(reasoningTokens);
            }
        }
        else
        {
            // Estimate tokens
            outputTokens = EstimateOutputTokens(response);
        }

        // If thinking content is provided but no reasoning tokens in usage, estimate
        if (thinking is not null && thinkingTokens == 0 && !string.IsNullOrEmpty(thinking.Text))
        {
            thinkingTokens = _tokenCounter.Count(thinking.Text);
        }

        lock (_lock)
        {
            _outputTokens += outputTokens;
            _thinkingTokens += thinkingTokens;
        }
    }

    /// <inheritdoc />
    public void SetInputTokens(int inputTokens)
    {
        lock (_lock)
        {
            _inputTokens = inputTokens;
        }
    }

    /// <inheritdoc />
    public BudgetUsage GetUsage()
    {
        lock (_lock)
        {
            return new BudgetUsage
            {
                InputTokens = _inputTokens,
                ThinkingTokens = _thinkingTokens,
                OutputTokens = _outputTokens
            };
        }
    }

    /// <inheritdoc />
    public bool IsThinkingBudgetExceeded(BudgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        lock (_lock)
        {
            return _thinkingTokens > config.ThinkingBudget;
        }
    }

    /// <inheritdoc />
    public bool IsAnswerBudgetExceeded(BudgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        lock (_lock)
        {
            return _outputTokens > config.AnswerBudget;
        }
    }

    /// <inheritdoc />
    public void Reset()
    {
        lock (_lock)
        {
            _inputTokens = 0;
            _thinkingTokens = 0;
            _outputTokens = 0;
        }
    }

    private int EstimateOutputTokens(ChatResponse response)
    {
        var text = response.Text;
        return string.IsNullOrEmpty(text) ? 0 : _tokenCounter.Count(text);
    }
}
