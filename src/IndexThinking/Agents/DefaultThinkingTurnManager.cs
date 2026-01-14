using IndexThinking.Abstractions;
using IndexThinking.Core;
using Microsoft.Extensions.AI;

namespace IndexThinking.Agents;

/// <summary>
/// Default implementation of <see cref="IThinkingTurnManager"/>.
/// </summary>
/// <remarks>
/// Orchestrates all components:
/// 1. Estimate complexity (if not provided)
/// 2. Send initial request
/// 3. Handle continuation if needed
/// 4. Parse reasoning content
/// 5. Track budget usage
/// 6. Store state
/// 7. Return combined result
/// </remarks>
public sealed class DefaultThinkingTurnManager : IThinkingTurnManager
{
    private readonly IComplexityEstimator _complexityEstimator;
    private readonly IContinuationHandler _continuationHandler;
    private readonly IBudgetTracker _budgetTracker;
    private readonly IReadOnlyList<IReasoningParser> _parsers;
    private readonly IThinkingStateStore? _stateStore;
    private readonly ITokenCounter _tokenCounter;

    /// <summary>
    /// Creates a new thinking turn manager.
    /// </summary>
    public DefaultThinkingTurnManager(
        IComplexityEstimator complexityEstimator,
        IContinuationHandler continuationHandler,
        IBudgetTracker budgetTracker,
        IEnumerable<IReasoningParser> parsers,
        ITokenCounter tokenCounter,
        IThinkingStateStore? stateStore = null)
    {
        _complexityEstimator = complexityEstimator ?? throw new ArgumentNullException(nameof(complexityEstimator));
        _continuationHandler = continuationHandler ?? throw new ArgumentNullException(nameof(continuationHandler));
        _budgetTracker = budgetTracker ?? throw new ArgumentNullException(nameof(budgetTracker));
        _parsers = parsers?.ToList() ?? throw new ArgumentNullException(nameof(parsers));
        _tokenCounter = tokenCounter ?? throw new ArgumentNullException(nameof(tokenCounter));
        _stateStore = stateStore;
    }

    /// <inheritdoc />
    public async Task<TurnResult> ProcessTurnAsync(
        ThinkingContext context,
        Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>> sendRequest)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(sendRequest);

        var metricsBuilder = TurnMetrics.CreateBuilder();

        try
        {
            // 1. Estimate complexity if not provided
            var complexity = context.EstimatedComplexity
                ?? _complexityEstimator.Estimate(context.Messages.ToList());

            metricsBuilder.WithComplexity(complexity);

            // 2. Calculate input tokens
            var inputTokens = CalculateInputTokens(context.Messages);
            metricsBuilder.WithInputTokens(inputTokens);
            _budgetTracker.SetInputTokens(inputTokens);

            // 3. Send initial request
            context.CancellationToken.ThrowIfCancellationRequested();
            var initialResponse = await sendRequest(context.Messages, context.CancellationToken);

            // 4. Handle continuation if needed
            var continuationResult = await _continuationHandler.HandleAsync(
                context, initialResponse, sendRequest);

            for (var i = 0; i < continuationResult.ContinuationCount; i++)
            {
                metricsBuilder.IncrementContinuation();
            }

            // 5. Parse reasoning from final response
            var (thinkingContent, reasoningState) = ParseReasoning(continuationResult.FinalResponse);

            // 6. Record metrics
            _budgetTracker.RecordResponse(continuationResult.FinalResponse, thinkingContent);
            var usage = _budgetTracker.GetUsage();
            metricsBuilder.AddThinkingTokens(usage.ThinkingTokens);
            metricsBuilder.AddOutputTokens(usage.OutputTokens);

            // 7. Store state if store is available
            if (_stateStore is not null)
            {
                await StoreStateAsync(context, usage, continuationResult, reasoningState);
            }

            // 8. Build and return result
            var metrics = metricsBuilder.Build();

            return continuationResult.ReachedMaxContinuations
                ? TurnResult.Truncated(continuationResult.FinalResponse, metrics, thinkingContent, reasoningState)
                : TurnResult.Success(continuationResult.FinalResponse, metrics, thinkingContent, reasoningState);
        }
        catch (OperationCanceledException)
        {
            // Clean up state on cancellation
            _budgetTracker.Reset();
            throw;
        }
    }

    private int CalculateInputTokens(IEnumerable<ChatMessage> messages)
    {
        var total = 0;
        foreach (var message in messages)
        {
            total += _tokenCounter.Count(message);
        }
        return total;
    }

    private (ThinkingContent?, ReasoningState?) ParseReasoning(ChatResponse response)
    {
        ThinkingContent? thinkingContent = null;
        ReasoningState? reasoningState = null;

        foreach (var parser in _parsers)
        {
            if (parser.TryParse(response, out var content) && content is not null)
            {
                thinkingContent = content;
            }

            var state = parser.ExtractState(response);
            if (state is not null)
            {
                reasoningState = state;
            }

            // Stop at first successful parse
            if (thinkingContent is not null || reasoningState is not null)
            {
                break;
            }
        }

        return (thinkingContent, reasoningState);
    }

    private async Task StoreStateAsync(
        ThinkingContext context,
        BudgetUsage usage,
        ContinuationResult continuationResult,
        ReasoningState? reasoningState)
    {
        if (_stateStore is null) return;

        var existingState = await _stateStore.GetAsync(context.SessionId, context.CancellationToken);

        var newState = new ThinkingState
        {
            SessionId = context.SessionId,
            ModelId = context.ModelId,
            ReasoningState = reasoningState,
            TotalThinkingTokens = (existingState?.TotalThinkingTokens ?? 0) + usage.ThinkingTokens,
            TotalOutputTokens = (existingState?.TotalOutputTokens ?? 0) + usage.OutputTokens,
            ContinuationCount = (existingState?.ContinuationCount ?? 0) + continuationResult.ContinuationCount,
            CreatedAt = existingState?.CreatedAt ?? DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _stateStore.SetAsync(context.SessionId, newState, context.CancellationToken);
    }
}
