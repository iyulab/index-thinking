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
    private readonly ITokenCounter _tokenCounter;
    private readonly DefaultContinuationHandler _handler;

    public DefaultContinuationHandlerTests()
    {
        _truncationDetector = Substitute.For<ITruncationDetector>();
        _tokenCounter = Substitute.For<ITokenCounter>();
        // Default: each message ~10 tokens, each string ~5 tokens
        _tokenCounter.Count(Arg.Any<ChatMessage>()).Returns(10);
        _tokenCounter.Count(Arg.Any<string>()).Returns(5);
        _handler = new DefaultContinuationHandler(_truncationDetector, _tokenCounter);
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
        Assert.Throws<ArgumentNullException>(() =>
            new DefaultContinuationHandler(null!, _tokenCounter));
    }

    [Fact]
    public void Constructor_NullTokenCounter_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DefaultContinuationHandler(_truncationDetector, null!));
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

    [Fact]
    public async Task HandleAsync_TruncatedWithThinkTags_StripsTagsFromContinuationContext()
    {
        // Arrange
        var context = CreateContext();
        var initialResponse = CreateResponse("<think>reasoning about the task</think>\n\nFirst part of answer");
        var continuationResponse = CreateResponse("Second part of answer");

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

        IList<ChatMessage>? capturedMessages = null;
        var sendRequest = Substitute.For<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>();
        sendRequest(Arg.Any<IList<ChatMessage>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedMessages = callInfo.ArgAt<IList<ChatMessage>>(0);
                return Task.FromResult(continuationResponse);
            });

        // Act
        await _handler.HandleAsync(context, initialResponse, sendRequest);

        // Assert - the assistant message in continuation should NOT contain think tags
        Assert.NotNull(capturedMessages);
        var assistantMessage = capturedMessages!.FirstOrDefault(m => m.Role == ChatRole.Assistant);
        Assert.NotNull(assistantMessage);
        Assert.DoesNotContain("<think>", assistantMessage!.Text);
        Assert.DoesNotContain("</think>", assistantMessage.Text);
        Assert.Contains("First part of answer", assistantMessage.Text);
    }

    [Fact]
    public async Task HandleAsync_MaxContextTokensExceeded_TriesCompactThenStops()
    {
        // Arrange: set MaxContextTokens very low so even compact messages exceed it
        var context = CreateContext(maxContextTokens: 50);
        var response = CreateResponseWithUsage("Truncated response with many tokens", promptTokens: 40);

        _truncationDetector
            .Detect(Arg.Any<ChatResponse>())
            .Returns(TruncationInfo.Truncated(TruncationReason.TokenLimit));

        // Token counter returns values that push over even for compact messages
        // Full estimate: promptTokenBaseline (40) + 30 + 30 = 100 > 50 → triggers compact
        // Compact estimate: each ChatMessage = 30 tokens, 2-3 messages → 60-90 > 50 → stops
        _tokenCounter.Count(Arg.Any<string>()).Returns(30);
        _tokenCounter.Count(Arg.Any<ChatMessage>()).Returns(30);

        var sendRequest = CreateSendRequestSubstitute(CreateResponse("More content"));

        // Act
        var result = await _handler.HandleAsync(context, response, sendRequest);

        // Assert - compact messages also exceed budget, so stops
        Assert.Equal(0, result.ContinuationCount);
        Assert.False(result.ReachedMaxContinuations);
    }

    [Fact]
    public async Task HandleAsync_MaxContextTokensNotSet_ContinuesNormally()
    {
        // Arrange: MaxContextTokens is null (default) - should not block continuation
        var context = CreateContext(); // no MaxContextTokens set
        var initialResponse = CreateResponse("First part truncated");
        var continuationResponse = CreateResponse("Second part complete");

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

        // Assert - continuation proceeds normally
        Assert.Equal(1, result.ContinuationCount);
    }

    [Fact]
    public async Task HandleAsync_MaxContextTokensWithFallbackEstimation_StopsWhenExceeded()
    {
        // Arrange: no usage data on response, so falls back to ITokenCounter estimation
        var context = CreateContext(maxContextTokens: 30);
        var response = CreateResponse("Truncated");

        _truncationDetector
            .Detect(Arg.Any<ChatResponse>())
            .Returns(TruncationInfo.Truncated(TruncationReason.TokenLimit));

        // Each message estimates to 20 tokens — both full and compact exceed budget
        // Full: 3 messages × 20 = 60 > 30 → triggers compact
        // Compact: 2 messages × 20 = 40 > 30 → stops
        _tokenCounter.Count(Arg.Any<ChatMessage>()).Returns(20);

        var sendRequest = CreateSendRequestSubstitute(CreateResponse("More"));

        // Act
        var result = await _handler.HandleAsync(context, response, sendRequest);

        // Assert - even compact estimation exceeds budget, should stop
        Assert.Equal(0, result.ContinuationCount);
    }

    [Fact]
    public async Task HandleAsync_MaxTotalDurationExceeded_StopsContinuation()
    {
        // Arrange: set MaxTotalDuration to zero so it immediately expires
        var config = new ContinuationConfig
        {
            MaxContinuations = 5,
            MaxTotalDuration = TimeSpan.Zero,
            MinProgressPerContinuation = 10
        };
        var context = ThinkingContext.Create("test-session", [new ChatMessage(ChatRole.User, "Test")])
            with { Continuation = config };

        var response = CreateResponse("Truncated content that is long enough");

        _truncationDetector
            .Detect(Arg.Any<ChatResponse>())
            .Returns(TruncationInfo.Truncated(TruncationReason.TokenLimit));

        var sendRequest = CreateSendRequestSubstitute(CreateResponse("More content here"));

        // Act
        var result = await _handler.HandleAsync(context, response, sendRequest);

        // Assert - should stop immediately due to duration exceeded
        Assert.Equal(0, result.ContinuationCount);
    }

    [Fact]
    public void ContinuationConfig_MaxContextTokens_ValidationRejectsZero()
    {
        // Arrange
        var config = new ContinuationConfig { MaxContextTokens = 0 };

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => config.Validate());
    }

    [Fact]
    public void ContinuationConfig_MaxContextTokens_ValidationRejectsNegative()
    {
        // Arrange
        var config = new ContinuationConfig { MaxContextTokens = -1 };

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => config.Validate());
    }

    [Fact]
    public void ContinuationConfig_MaxContextTokens_ValidationAcceptsNull()
    {
        // Arrange
        var config = new ContinuationConfig { MaxContextTokens = null };

        // Act & Assert (should not throw)
        config.Validate();
    }

    [Fact]
    public void ContinuationConfig_MaxContextTokens_ValidationAcceptsPositive()
    {
        // Arrange
        var config = new ContinuationConfig { MaxContextTokens = 4096 };

        // Act & Assert (should not throw)
        config.Validate();
    }

    [Fact]
    public async Task HandleAsync_UsesPromptTokenBaselineWhenAvailable()
    {
        // Arrange: response has usage with prompt_tokens
        var context = CreateContext(maxContextTokens: 100);
        var response = CreateResponseWithUsage("First part truncated", promptTokens: 60);
        var continuationResponse = CreateResponse("Second part");

        // First call truncated, second not
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

        // With promptTokenBaseline=60, assistant text tokens=15, continuation prompt tokens=15
        // Total = 60 + 15 + 15 = 90 < 100, so continuation should proceed
        _tokenCounter.Count(Arg.Any<string>()).Returns(15);

        var sendRequest = Substitute.For<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>();
        sendRequest(Arg.Any<IList<ChatMessage>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(continuationResponse));

        // Act
        var result = await _handler.HandleAsync(context, response, sendRequest);

        // Assert - continuation proceeds because within budget
        Assert.Equal(1, result.ContinuationCount);
    }

    private static ThinkingContext CreateContext(
        int maxContinuations = 5,
        bool throwOnMax = false,
        int? maxContextTokens = null)
    {
        return ThinkingContext.Create("test-session", [new ChatMessage(ChatRole.User, "Test")])
            with
            {
                Continuation = new ContinuationConfig
                {
                    MaxContinuations = maxContinuations,
                    ThrowOnMaxContinuations = throwOnMax,
                    MinProgressPerContinuation = 10,
                    MaxContextTokens = maxContextTokens
                }
            };
    }

    private static ChatResponse CreateResponse(string text)
    {
        var message = new ChatMessage(ChatRole.Assistant, text);
        return new ChatResponse([message]);
    }

    private static ChatResponse CreateResponseWithUsage(string text, int promptTokens)
    {
        var message = new ChatMessage(ChatRole.Assistant, text);
        return new ChatResponse([message])
        {
            Usage = new UsageDetails
            {
                InputTokenCount = promptTokens,
                OutputTokenCount = text.Length / 4
            }
        };
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
