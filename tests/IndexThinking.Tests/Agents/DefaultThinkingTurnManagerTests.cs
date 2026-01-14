using IndexThinking.Abstractions;
using IndexThinking.Agents;
using IndexThinking.Core;
using Microsoft.Extensions.AI;
using Moq;
using Xunit;

namespace IndexThinking.Tests.Agents;

public class DefaultThinkingTurnManagerTests
{
    private readonly Mock<IComplexityEstimator> _complexityEstimatorMock;
    private readonly Mock<IContinuationHandler> _continuationHandlerMock;
    private readonly Mock<IBudgetTracker> _budgetTrackerMock;
    private readonly Mock<IReasoningParser> _parserMock;
    private readonly Mock<ITokenCounter> _tokenCounterMock;
    private readonly Mock<IThinkingStateStore> _stateStoreMock;
    private readonly DefaultThinkingTurnManager _manager;

    public DefaultThinkingTurnManagerTests()
    {
        _complexityEstimatorMock = new Mock<IComplexityEstimator>();
        _continuationHandlerMock = new Mock<IContinuationHandler>();
        _budgetTrackerMock = new Mock<IBudgetTracker>();
        _parserMock = new Mock<IReasoningParser>();
        _tokenCounterMock = new Mock<ITokenCounter>();
        _stateStoreMock = new Mock<IThinkingStateStore>();

        // Default setups
        _complexityEstimatorMock
            .Setup(x => x.Estimate(It.IsAny<IReadOnlyList<ChatMessage>>()))
            .Returns(TaskComplexity.Moderate);

        _tokenCounterMock
            .Setup(x => x.Count(It.IsAny<ChatMessage>()))
            .Returns(50);

        _budgetTrackerMock
            .Setup(x => x.GetUsage())
            .Returns(new BudgetUsage { InputTokens = 50, OutputTokens = 100, ThinkingTokens = 200 });

        _manager = new DefaultThinkingTurnManager(
            _complexityEstimatorMock.Object,
            _continuationHandlerMock.Object,
            _budgetTrackerMock.Object,
            [_parserMock.Object],
            _tokenCounterMock.Object,
            _stateStoreMock.Object);
    }

    [Fact]
    public async Task ProcessTurnAsync_SimpleRequest_CompletesWithoutContinuation()
    {
        // Arrange
        var context = CreateContext();
        var response = CreateResponse("Simple response");

        _continuationHandlerMock
            .Setup(x => x.HandleAsync(context, response, It.IsAny<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>()))
            .ReturnsAsync(ContinuationResult.NotTruncated(response));

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

        _continuationHandlerMock
            .Setup(x => x.HandleAsync(context, initialResponse, It.IsAny<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>()))
            .ReturnsAsync(continuationResult);

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

        _continuationHandlerMock
            .Setup(x => x.HandleAsync(context, response, It.IsAny<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>()))
            .ReturnsAsync(ContinuationResult.NotTruncated(response));

        _parserMock
            .Setup(x => x.TryParse(response, out thinking))
            .Returns(true);

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

        _continuationHandlerMock
            .Setup(x => x.HandleAsync(context, response, It.IsAny<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>()))
            .ReturnsAsync(ContinuationResult.NotTruncated(response));

        _stateStoreMock
            .Setup(x => x.GetAsync(context.SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ThinkingState?)null);

        // Act
        await _manager.ProcessTurnAsync(context, (_, _) => Task.FromResult(response));

        // Assert
        _stateStoreMock.Verify(x => x.SetAsync(
            context.SessionId,
            It.IsAny<ThinkingState>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessTurnAsync_TracksMetrics_Accurately()
    {
        // Arrange
        var context = CreateContext();
        var response = CreateResponse("Response");

        _continuationHandlerMock
            .Setup(x => x.HandleAsync(context, response, It.IsAny<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>()))
            .ReturnsAsync(ContinuationResult.NotTruncated(response));

        _budgetTrackerMock
            .Setup(x => x.GetUsage())
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

        _budgetTrackerMock.Verify(x => x.Reset(), Times.Once);
    }

    [Fact]
    public async Task ProcessTurnAsync_UsesProvidedComplexity_SkipsEstimation()
    {
        // Arrange
        var context = CreateContext().WithComplexity(TaskComplexity.Research);
        var response = CreateResponse("Response");

        _continuationHandlerMock
            .Setup(x => x.HandleAsync(context, response, It.IsAny<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>()))
            .ReturnsAsync(ContinuationResult.NotTruncated(response));

        // Act
        var result = await _manager.ProcessTurnAsync(context, (_, _) => Task.FromResult(response));

        // Assert
        Assert.Equal(TaskComplexity.Research, result.Metrics.DetectedComplexity);
        _complexityEstimatorMock.Verify(x => x.Estimate(It.IsAny<IReadOnlyList<ChatMessage>>()), Times.Never);
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

        _continuationHandlerMock
            .Setup(x => x.HandleAsync(context, response, It.IsAny<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>()))
            .ReturnsAsync(continuationResult);

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
            _continuationHandlerMock.Object,
            _budgetTrackerMock.Object,
            [_parserMock.Object],
            _tokenCounterMock.Object));
    }

    [Fact]
    public void Constructor_NullContinuationHandler_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new DefaultThinkingTurnManager(
            _complexityEstimatorMock.Object,
            null!,
            _budgetTrackerMock.Object,
            [_parserMock.Object],
            _tokenCounterMock.Object));
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
