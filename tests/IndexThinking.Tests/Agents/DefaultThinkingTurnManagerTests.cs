using IndexThinking.Abstractions;
using IndexThinking.Agents;
using IndexThinking.Core;
using Microsoft.Extensions.AI;
using NSubstitute;
using Xunit;

namespace IndexThinking.Tests.Agents;

public class DefaultThinkingTurnManagerTests
{
    private readonly IComplexityEstimator _complexityEstimator;
    private readonly IContinuationHandler _continuationHandler;
    private readonly IBudgetTracker _budgetTracker;
    private readonly IReasoningParser _parser;
    private readonly ITokenCounter _tokenCounter;
    private readonly IThinkingStateStore _stateStore;
    private readonly DefaultThinkingTurnManager _manager;

    public DefaultThinkingTurnManagerTests()
    {
        _complexityEstimator = Substitute.For<IComplexityEstimator>();
        _continuationHandler = Substitute.For<IContinuationHandler>();
        _budgetTracker = Substitute.For<IBudgetTracker>();
        _parser = Substitute.For<IReasoningParser>();
        _tokenCounter = Substitute.For<ITokenCounter>();
        _stateStore = Substitute.For<IThinkingStateStore>();

        // Default setups
        _complexityEstimator
            .Estimate(Arg.Any<IReadOnlyList<ChatMessage>>())
            .Returns(TaskComplexity.Moderate);

        _tokenCounter
            .Count(Arg.Any<ChatMessage>())
            .Returns(50);

        _budgetTracker
            .GetUsage()
            .Returns(new BudgetUsage { InputTokens = 50, OutputTokens = 100, ThinkingTokens = 200 });

        _manager = new DefaultThinkingTurnManager(
            _complexityEstimator,
            _continuationHandler,
            _budgetTracker,
            [_parser],
            _tokenCounter,
            _stateStore);
    }

    [Fact]
    public async Task ProcessTurnAsync_SimpleRequest_CompletesWithoutContinuation()
    {
        // Arrange
        var context = CreateContext();
        var response = CreateResponse("Simple response");

        _continuationHandler
            .HandleAsync(context, response, Arg.Any<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>())
            .Returns(Task.FromResult(ContinuationResult.NotTruncated(response)));

        // Act
        var result = await _manager.ProcessTurnAsync(context, (_, _) => Task.FromResult(response));

        // Assert
        Assert.Same(response, result.Response);
        Assert.False(result.WasTruncated);
        Assert.False(result.WasContinued);
    }

    [Fact]
    public async Task ProcessTurnAsync_TruncatedResponse_ContinuesSuccessfully()
    {
        // Arrange
        var context = CreateContext();
        var initialResponse = CreateResponse("Initial");
        var finalResponse = CreateResponse("Combined response");

        var continuationResult = new ContinuationResult
        {
            FinalResponse = finalResponse,
            ContinuationCount = 2,
            ReachedMaxContinuations = false
        };

        _continuationHandler
            .HandleAsync(context, initialResponse, Arg.Any<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>())
            .Returns(Task.FromResult(continuationResult));

        // Act
        var result = await _manager.ProcessTurnAsync(context, (_, _) => Task.FromResult(initialResponse));

        // Assert
        Assert.Same(finalResponse, result.Response);
        Assert.True(result.WasContinued);
        Assert.Equal(2, result.Metrics.ContinuationCount);
    }

    [Fact]
    public async Task ProcessTurnAsync_WithThinkingContent_ParsesCorrectly()
    {
        // Arrange
        var context = CreateContext();
        var response = CreateResponse("Response with thinking");
        var thinking = new ThinkingContent { Text = "My reasoning process" };

        _continuationHandler
            .HandleAsync(context, response, Arg.Any<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>())
            .Returns(Task.FromResult(ContinuationResult.NotTruncated(response)));

        _parser
            .TryParse(response, out Arg.Any<ThinkingContent?>())
            .Returns(callInfo =>
            {
                callInfo[1] = thinking;
                return true;
            });

        // Act
        var result = await _manager.ProcessTurnAsync(context, (_, _) => Task.FromResult(response));

        // Assert
        Assert.True(result.HasThinkingContent);
        Assert.Equal("My reasoning process", result.ThinkingContent?.Text);
    }

    [Fact]
    public async Task ProcessTurnAsync_StoresState_InStateStore()
    {
        // Arrange
        var context = CreateContext();
        var response = CreateResponse("Response");

        _continuationHandler
            .HandleAsync(context, response, Arg.Any<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>())
            .Returns(Task.FromResult(ContinuationResult.NotTruncated(response)));

        _stateStore
            .GetAsync(context.SessionId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult((ThinkingState?)null));

        // Act
        await _manager.ProcessTurnAsync(context, (_, _) => Task.FromResult(response));

        // Assert
        await _stateStore.Received(1).SetAsync(
            context.SessionId,
            Arg.Any<ThinkingState>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessTurnAsync_TracksMetrics_Accurately()
    {
        // Arrange
        var context = CreateContext();
        var response = CreateResponse("Response");

        _continuationHandler
            .HandleAsync(context, response, Arg.Any<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>())
            .Returns(Task.FromResult(ContinuationResult.NotTruncated(response)));

        _budgetTracker
            .GetUsage()
            .Returns(new BudgetUsage { InputTokens = 50, OutputTokens = 150, ThinkingTokens = 300 });

        // Act
        var result = await _manager.ProcessTurnAsync(context, (_, _) => Task.FromResult(response));

        // Assert
        Assert.Equal(50, result.Metrics.InputTokens);
        Assert.Equal(150, result.Metrics.OutputTokens);
        Assert.Equal(300, result.Metrics.ThinkingTokens);
        Assert.Equal(500, result.Metrics.TotalTokens);
    }

    [Fact]
    public async Task ProcessTurnAsync_Cancelled_ThrowsAndCleansUp()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var context = CreateContext().WithCancellation(cts.Token);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _manager.ProcessTurnAsync(context, (_, _) => Task.FromResult(CreateResponse("Response"))));

        _budgetTracker.Received(1).Reset();
    }

    [Fact]
    public async Task ProcessTurnAsync_UsesProvidedComplexity_SkipsEstimation()
    {
        // Arrange
        var context = CreateContext().WithComplexity(TaskComplexity.Research);
        var response = CreateResponse("Response");

        _continuationHandler
            .HandleAsync(context, response, Arg.Any<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>())
            .Returns(Task.FromResult(ContinuationResult.NotTruncated(response)));

        // Act
        var result = await _manager.ProcessTurnAsync(context, (_, _) => Task.FromResult(response));

        // Assert
        Assert.Equal(TaskComplexity.Research, result.Metrics.DetectedComplexity);
        _complexityEstimator.DidNotReceive().Estimate(Arg.Any<IReadOnlyList<ChatMessage>>());
    }

    [Fact]
    public async Task ProcessTurnAsync_ReachedMaxContinuations_ReturnsTruncatedResult()
    {
        // Arrange
        var context = CreateContext();
        var response = CreateResponse("Partial");

        var continuationResult = new ContinuationResult
        {
            FinalResponse = response,
            ContinuationCount = 5,
            ReachedMaxContinuations = true
        };

        _continuationHandler
            .HandleAsync(context, response, Arg.Any<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>())
            .Returns(Task.FromResult(continuationResult));

        // Act
        var result = await _manager.ProcessTurnAsync(context, (_, _) => Task.FromResult(response));

        // Assert
        Assert.True(result.WasTruncated);
    }

    [Fact]
    public void Constructor_NullComplexityEstimator_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new DefaultThinkingTurnManager(
            null!,
            _continuationHandler,
            _budgetTracker,
            [_parser],
            _tokenCounter));
    }

    [Fact]
    public void Constructor_NullContinuationHandler_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new DefaultThinkingTurnManager(
            _complexityEstimator,
            null!,
            _budgetTracker,
            [_parser],
            _tokenCounter));
    }

    [Fact]
    public async Task ProcessTurnAsync_NullContext_Throws()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _manager.ProcessTurnAsync(null!, (_, _) => Task.FromResult(CreateResponse("Test"))));
    }

    private static ThinkingContext CreateContext()
    {
        return ThinkingContext.Create("test-session", [new ChatMessage(ChatRole.User, "Test message")]);
    }

    private static ChatResponse CreateResponse(string text)
    {
        var message = new ChatMessage(ChatRole.Assistant, text);
        return new ChatResponse([message]);
    }
}
