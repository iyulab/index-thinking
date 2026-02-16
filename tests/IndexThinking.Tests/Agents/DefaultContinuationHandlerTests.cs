using IndexThinking.Abstractions;
using IndexThinking.Agents;
using IndexThinking.Continuation;
using IndexThinking.Core;
using Microsoft.Extensions.AI;
using NSubstitute;
using Xunit;

namespace IndexThinking.Tests.Agents;

public class DefaultContinuationHandlerTests
{
    private readonly ITruncationDetector _truncationDetector;
    private readonly DefaultContinuationHandler _handler;

    public DefaultContinuationHandlerTests()
    {
        _truncationDetector = Substitute.For<ITruncationDetector>();
        _handler = new DefaultContinuationHandler(_truncationDetector);
    }

    [Fact]
    public async Task HandleAsync_NotTruncated_ReturnsImmediately()
    {
        // Arrange
        var context = CreateContext();
        var response = CreateResponse("Complete response");
        _truncationDetector
            .Detect(response)
            .Returns(TruncationInfo.NotTruncated);

        // Act
        var result = await _handler.HandleAsync(context, response, MockSendRequest);

        // Assert
        Assert.Same(response, result.FinalResponse);
        Assert.Equal(0, result.ContinuationCount);
        Assert.False(result.ReachedMaxContinuations);
    }

    [Fact]
    public async Task HandleAsync_TruncatedOnce_ContinuesAndCombines()
    {
        // Arrange
        var context = CreateContext();
        var initialResponse = CreateResponse("First part...");
        var continuationResponse = CreateResponse("...second part.");

        var callCount = 0;
        _truncationDetector
            .Detect(Arg.Any<ChatResponse>())
            .Returns(callInfo =>
            {
                callCount++;
                return callCount == 1
                    ? TruncationInfo.Truncated(TruncationReason.TokenLimit)
                    : TruncationInfo.NotTruncated;
            });

        var sendRequest = Substitute.For<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>();
        sendRequest(Arg.Any<IList<ChatMessage>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(continuationResponse));

        // Act
        var result = await _handler.HandleAsync(context, initialResponse, sendRequest);

        // Assert
        Assert.Equal(1, result.ContinuationCount);
        Assert.False(result.ReachedMaxContinuations);
        Assert.Contains("First part", result.FinalResponse.Text);
        Assert.Contains("second part", result.FinalResponse.Text);
    }

    [Fact]
    public async Task HandleAsync_MultipleTruncations_ContinuesUntilComplete()
    {
        // Arrange - use longer responses to exceed MinProgressPerContinuation
        var context = CreateContext();
        var initial = CreateResponse("Part 1: This is the initial content that was truncated");
        var second = CreateResponse("Part 2: This is the continuation with more content");
        var third = CreateResponse("Part 3: This is the final complete part");

        var detectCallCount = 0;
        _truncationDetector
            .Detect(Arg.Any<ChatResponse>())
            .Returns(callInfo =>
            {
                detectCallCount++;
                return detectCallCount <= 2
                    ? TruncationInfo.Truncated(TruncationReason.TokenLimit)
                    : TruncationInfo.NotTruncated;
            });

        var sendCallCount = 0;
        var sendRequest = Substitute.For<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>();
        sendRequest(Arg.Any<IList<ChatMessage>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                sendCallCount++;
                return Task.FromResult(sendCallCount == 1 ? second : third);
            });

        // Act
        var result = await _handler.HandleAsync(context, initial, sendRequest);

        // Assert
        Assert.Equal(2, result.ContinuationCount);
        Assert.False(result.ReachedMaxContinuations);
        Assert.Equal(3, result.IntermediateResponses.Count);
    }

    [Fact]
    public async Task HandleAsync_MaxContinuationsReached_StopsAndReturnsPartial()
    {
        // Arrange
        var context = CreateContext(maxContinuations: 2);
        var response = CreateResponse("Partial");

        _truncationDetector
            .Detect(Arg.Any<ChatResponse>())
            .Returns(TruncationInfo.Truncated(TruncationReason.TokenLimit));

        var sendRequest = CreateSendRequestSubstitute(CreateResponse("Still truncated"));

        // Act
        var result = await _handler.HandleAsync(context, response, sendRequest);

        // Assert
        Assert.Equal(2, result.ContinuationCount);
        Assert.True(result.ReachedMaxContinuations);
    }

    [Fact]
    public async Task HandleAsync_MaxContinuationsWithThrow_ThrowsException()
    {
        // Arrange
        var context = CreateContext(maxContinuations: 1, throwOnMax: true);
        var response = CreateResponse("Partial");

        _truncationDetector
            .Detect(Arg.Any<ChatResponse>())
            .Returns(TruncationInfo.Truncated(TruncationReason.TokenLimit));

        var sendRequest = CreateSendRequestSubstitute(CreateResponse("Still truncated"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.HandleAsync(context, response, sendRequest));
    }

    [Fact]
    public async Task HandleAsync_Cancelled_ThrowsOperationCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var context = CreateContext().WithCancellation(cts.Token);
        var response = CreateResponse("Partial");

        _truncationDetector
            .Detect(response)
            .Returns(TruncationInfo.Truncated(TruncationReason.TokenLimit));

        var sendRequest = CreateSendRequestSubstitute(CreateResponse("Next"));

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _handler.HandleAsync(context, response, sendRequest));
    }

    [Fact]
    public async Task HandleAsync_NoProgress_StopsEarly()
    {
        // Arrange - MinProgressPerContinuation defaults to 10
        var context = CreateContext();
        var response = CreateResponse("Initial response");

        _truncationDetector
            .Detect(Arg.Any<ChatResponse>())
            .Returns(TruncationInfo.Truncated(TruncationReason.TokenLimit));

        // Return very short responses that don't make progress
        var sendRequest = CreateSendRequestSubstitute(CreateResponse("X")); // Only 1 char, below MinProgressPerContinuation

        // Act
        var result = await _handler.HandleAsync(context, response, sendRequest);

        // Assert - Should stop after first continuation due to no progress
        Assert.Equal(1, result.ContinuationCount);
    }

    [Fact]
    public void Constructor_NullTruncationDetector_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new DefaultContinuationHandler(null!));
    }

    [Fact]
    public async Task HandleAsync_NullContext_Throws()
    {
        // Arrange
        var response = CreateResponse("Test");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.HandleAsync(null!, response, MockSendRequest));
    }

    [Fact]
    public async Task HandleAsync_NullResponse_Throws()
    {
        // Arrange
        var context = CreateContext();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.HandleAsync(context, null!, MockSendRequest));
    }

    [Fact]
    public async Task HandleAsync_NullSendRequest_Throws()
    {
        // Arrange
        var context = CreateContext();
        var response = CreateResponse("Test");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.HandleAsync(context, response, null!));
    }

    [Fact]
    public void ContinuationResult_NotTruncated_HasCorrectDefaults()
    {
        // Arrange
        var response = CreateResponse("Test");

        // Act
        var result = ContinuationResult.NotTruncated(response);

        // Assert
        Assert.Same(response, result.FinalResponse);
        Assert.Equal(0, result.ContinuationCount);
        Assert.False(result.ReachedMaxContinuations);
        Assert.Empty(result.IntermediateResponses);
    }

    private static ThinkingContext CreateContext(int maxContinuations = 5, bool throwOnMax = false)
    {
        return ThinkingContext.Create("test-session", [new ChatMessage(ChatRole.User, "Test")])
            with
            {
                Continuation = new ContinuationConfig
                {
                    MaxContinuations = maxContinuations,
                    ThrowOnMaxContinuations = throwOnMax,
                    MinProgressPerContinuation = 10
                }
            };
    }

    private static ChatResponse CreateResponse(string text)
    {
        var message = new ChatMessage(ChatRole.Assistant, text);
        return new ChatResponse([message]);
    }

    private static Task<ChatResponse> MockSendRequest(IList<ChatMessage> messages, CancellationToken ct)
    {
        return Task.FromResult(CreateResponse("Mock response"));
    }

    private static Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>> CreateSendRequestSubstitute(
        ChatResponse response)
    {
        var sendRequest = Substitute.For<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>();
        sendRequest(Arg.Any<IList<ChatMessage>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(response));
        return sendRequest;
    }
}
