using FluentAssertions;
using IndexThinking.Abstractions;
using IndexThinking.Agents;
using IndexThinking.Client;
using IndexThinking.Core;
using Microsoft.Extensions.AI;
using Moq;
using Xunit;

namespace IndexThinking.Tests.Client;

public class ThinkingChatClientTests
{
    private readonly Mock<IChatClient> _innerClientMock;
    private readonly Mock<IThinkingTurnManager> _turnManagerMock;
    private readonly ThinkingChatClient _client;

    public ThinkingChatClientTests()
    {
        _innerClientMock = new Mock<IChatClient>();
        _turnManagerMock = new Mock<IThinkingTurnManager>();
        _client = new ThinkingChatClient(
            _innerClientMock.Object,
            _turnManagerMock.Object);
    }

    [Fact]
    public async Task GetResponseAsync_DelegatesToTurnManager()
    {
        // Arrange
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello")
        };
        var expectedResponse = new ChatResponse([new ChatMessage(ChatRole.Assistant, "Hi")]);
        var turnResult = TurnResult.Success(expectedResponse, TurnMetrics.CreateBuilder().Build());

        _turnManagerMock
            .Setup(x => x.ProcessTurnAsync(
                It.IsAny<ThinkingContext>(),
                It.IsAny<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>()))
            .ReturnsAsync(turnResult);

        // Act
        var response = await _client.GetResponseAsync(messages);

        // Assert
        response.Should().NotBeNull();
        _turnManagerMock.Verify(x => x.ProcessTurnAsync(
            It.IsAny<ThinkingContext>(),
            It.IsAny<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetResponseAsync_IncludesThinkingContentInMetadata()
    {
        // Arrange
        var messages = new List<ChatMessage> { new(ChatRole.User, "Test") };
        var expectedResponse = new ChatResponse([new ChatMessage(ChatRole.Assistant, "Response")]);
        var thinkingContent = new ThinkingContent { Text = "My reasoning" };
        var turnResult = TurnResult.Success(expectedResponse, TurnMetrics.CreateBuilder().Build(), thinkingContent);

        _turnManagerMock
            .Setup(x => x.ProcessTurnAsync(
                It.IsAny<ThinkingContext>(),
                It.IsAny<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>()))
            .ReturnsAsync(turnResult);

        // Act
        var response = await _client.GetResponseAsync(messages);

        // Assert
        var thinking = response.GetThinkingContent();
        thinking.Should().NotBeNull();
        thinking!.Text.Should().Be("My reasoning");
    }

    [Fact]
    public async Task GetResponseAsync_IncludesTurnMetricsInMetadata()
    {
        // Arrange
        var messages = new List<ChatMessage> { new(ChatRole.User, "Test") };
        var expectedResponse = new ChatResponse([new ChatMessage(ChatRole.Assistant, "Response")]);
        var metrics = TurnMetrics.CreateBuilder()
            .WithInputTokens(100)
            .AddOutputTokens(200)
            .AddThinkingTokens(300)
            .Build();
        var turnResult = TurnResult.Success(expectedResponse, metrics);

        _turnManagerMock
            .Setup(x => x.ProcessTurnAsync(
                It.IsAny<ThinkingContext>(),
                It.IsAny<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>()))
            .ReturnsAsync(turnResult);

        // Act
        var response = await _client.GetResponseAsync(messages);

        // Assert
        var returnedMetrics = response.GetTurnMetrics();
        returnedMetrics.Should().NotBeNull();
        returnedMetrics!.InputTokens.Should().Be(100);
        returnedMetrics.OutputTokens.Should().Be(200);
        returnedMetrics.ThinkingTokens.Should().Be(300);
    }

    [Fact]
    public async Task GetResponseAsync_IncludesTurnResultInMetadata()
    {
        // Arrange
        var messages = new List<ChatMessage> { new(ChatRole.User, "Test") };
        var expectedResponse = new ChatResponse([new ChatMessage(ChatRole.Assistant, "Response")]);
        var turnResult = TurnResult.Truncated(expectedResponse, TurnMetrics.CreateBuilder().Build());

        _turnManagerMock
            .Setup(x => x.ProcessTurnAsync(
                It.IsAny<ThinkingContext>(),
                It.IsAny<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>()))
            .ReturnsAsync(turnResult);

        // Act
        var response = await _client.GetResponseAsync(messages);

        // Assert
        var result = response.GetTurnResult();
        result.Should().NotBeNull();
        result!.WasTruncated.Should().BeTrue();
    }

    [Fact]
    public async Task GetResponseAsync_CreatesContextWithSessionId()
    {
        // Arrange
        var messages = new List<ChatMessage> { new(ChatRole.User, "Test") };
        var expectedResponse = new ChatResponse([new ChatMessage(ChatRole.Assistant, "Response")]);
        var turnResult = TurnResult.Success(expectedResponse, TurnMetrics.CreateBuilder().Build());
        ThinkingContext? capturedContext = null;

        _turnManagerMock
            .Setup(x => x.ProcessTurnAsync(
                It.IsAny<ThinkingContext>(),
                It.IsAny<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>()))
            .Callback<ThinkingContext, Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>(
                (ctx, _) => capturedContext = ctx)
            .ReturnsAsync(turnResult);

        // Act
        await _client.GetResponseAsync(messages);

        // Assert
        capturedContext.Should().NotBeNull();
        capturedContext!.SessionId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetResponseAsync_UsesProvidedSessionId()
    {
        // Arrange
        var messages = new List<ChatMessage> { new(ChatRole.User, "Test") };
        var options = new ChatOptions
        {
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                ["IndexThinking.SessionId"] = "my-custom-session"
            }
        };
        var expectedResponse = new ChatResponse([new ChatMessage(ChatRole.Assistant, "Response")]);
        var turnResult = TurnResult.Success(expectedResponse, TurnMetrics.CreateBuilder().Build());
        ThinkingContext? capturedContext = null;

        _turnManagerMock
            .Setup(x => x.ProcessTurnAsync(
                It.IsAny<ThinkingContext>(),
                It.IsAny<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>()))
            .Callback<ThinkingContext, Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>(
                (ctx, _) => capturedContext = ctx)
            .ReturnsAsync(turnResult);

        // Act
        await _client.GetResponseAsync(messages, options);

        // Assert
        capturedContext.Should().NotBeNull();
        capturedContext!.SessionId.Should().Be("my-custom-session");
    }

    [Fact]
    public async Task GetResponseAsync_PassesModelIdToContext()
    {
        // Arrange
        var messages = new List<ChatMessage> { new(ChatRole.User, "Test") };
        var options = new ChatOptions { ModelId = "gpt-4" };
        var expectedResponse = new ChatResponse([new ChatMessage(ChatRole.Assistant, "Response")]);
        var turnResult = TurnResult.Success(expectedResponse, TurnMetrics.CreateBuilder().Build());
        ThinkingContext? capturedContext = null;

        _turnManagerMock
            .Setup(x => x.ProcessTurnAsync(
                It.IsAny<ThinkingContext>(),
                It.IsAny<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>()))
            .Callback<ThinkingContext, Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>(
                (ctx, _) => capturedContext = ctx)
            .ReturnsAsync(turnResult);

        // Act
        await _client.GetResponseAsync(messages, options);

        // Assert
        capturedContext.Should().NotBeNull();
        capturedContext!.ModelId.Should().Be("gpt-4");
    }

    [Fact]
    public async Task GetResponseAsync_NullMessages_Throws()
    {
        // Act
        var action = () => _client.GetResponseAsync(null!);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullInnerClient_Throws()
    {
        // Act
        var action = () => new ThinkingChatClient(null!, _turnManagerMock.Object);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullTurnManager_Throws()
    {
        // Act
        var action = () => new ThinkingChatClient(_innerClientMock.Object, null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task GetStreamingResponseAsync_DelegatesToInnerClient()
    {
        // Arrange
        var messages = new List<ChatMessage> { new(ChatRole.User, "Test") };
        var updates = new List<ChatResponseUpdate>
        {
            new() { Contents = [new TextContent("Hello ")] },
            new() { Contents = [new TextContent("World")] }
        };

        _innerClientMock
            .Setup(x => x.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns(updates.ToAsyncEnumerable());

        // Act
        var result = new List<ChatResponseUpdate>();
        await foreach (var update in _client.GetStreamingResponseAsync(messages))
        {
            result.Add(update);
        }

        // Assert
        result.Should().HaveCount(2);
        result[0].Text.Should().Be("Hello ");
        result[1].Text.Should().Be("World");
    }
}

public class ThinkingChatClientOptionsTests
{
    [Fact]
    public void DefaultOptions_HasCorrectDefaults()
    {
        // Arrange & Act
        var options = new ThinkingChatClientOptions();

        // Assert
        options.AutoEstimateComplexity.Should().BeTrue();
        options.IncludeThinkingInMetadata.Should().BeTrue();
        options.IncludeMetricsInMetadata.Should().BeTrue();
        options.SessionIdKey.Should().Be("IndexThinking.SessionId");
        options.SessionIdFactory.Should().NotBeNull();
        options.SessionIdFactory().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void SessionIdFactory_GeneratesUniqueIds()
    {
        // Arrange
        var options = new ThinkingChatClientOptions();

        // Act
        var id1 = options.SessionIdFactory();
        var id2 = options.SessionIdFactory();

        // Assert
        id1.Should().NotBe(id2);
    }
}

public class ThinkingChatClientExtensionsTests
{
    [Fact]
    public void GetThinkingContent_ReturnsNull_WhenNotPresent()
    {
        // Arrange
        var response = new ChatResponse([new ChatMessage(ChatRole.Assistant, "Test")]);

        // Act
        var result = response.GetThinkingContent();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetTurnMetrics_ReturnsNull_WhenNotPresent()
    {
        // Arrange
        var response = new ChatResponse([new ChatMessage(ChatRole.Assistant, "Test")]);

        // Act
        var result = response.GetTurnMetrics();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetTurnResult_ReturnsNull_WhenNotPresent()
    {
        // Arrange
        var response = new ChatResponse([new ChatMessage(ChatRole.Assistant, "Test")]);

        // Act
        var result = response.GetTurnResult();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetThinkingContent_NullResponse_Throws()
    {
        // Act
        var action = () => ThinkingChatClientExtensions.GetThinkingContent(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetTurnMetrics_NullResponse_Throws()
    {
        // Act
        var action = () => ThinkingChatClientExtensions.GetTurnMetrics(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetTurnResult_NullResponse_Throws()
    {
        // Act
        var action = () => ThinkingChatClientExtensions.GetTurnResult(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }
}
