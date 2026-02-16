using FluentAssertions;
using IndexThinking.Abstractions;
using IndexThinking.Agents;
using IndexThinking.Client;
using IndexThinking.Context;
using IndexThinking.Core;
using Microsoft.Extensions.AI;
using NSubstitute;
using Xunit;

namespace IndexThinking.Tests.Client;

public class ThinkingChatClientTests
{
    private readonly IChatClient _innerClient;
    private readonly IThinkingTurnManager _turnManager;
    private readonly ThinkingChatClient _client;

    public ThinkingChatClientTests()
    {
        _innerClient = Substitute.For<IChatClient>();
        _turnManager = Substitute.For<IThinkingTurnManager>();
        _client = new ThinkingChatClient(
            _innerClient,
            _turnManager);
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

        _turnManager
            .ProcessTurnAsync(
                Arg.Any<ThinkingContext>(),
                Arg.Any<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>())
            .Returns(Task.FromResult(turnResult));

        // Act
        var response = await _client.GetResponseAsync(messages);

        // Assert
        response.Should().NotBeNull();
        await _turnManager.Received(1).ProcessTurnAsync(
            Arg.Any<ThinkingContext>(),
            Arg.Any<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>());
    }

    [Fact]
    public async Task GetResponseAsync_IncludesThinkingContentInMetadata()
    {
        // Arrange
        var messages = new List<ChatMessage> { new(ChatRole.User, "Test") };
        var expectedResponse = new ChatResponse([new ChatMessage(ChatRole.Assistant, "Response")]);
        var thinkingContent = new ThinkingContent { Text = "My reasoning" };
        var turnResult = TurnResult.Success(expectedResponse, TurnMetrics.CreateBuilder().Build(), thinkingContent);

        _turnManager
            .ProcessTurnAsync(
                Arg.Any<ThinkingContext>(),
                Arg.Any<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>())
            .Returns(Task.FromResult(turnResult));

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

        _turnManager
            .ProcessTurnAsync(
                Arg.Any<ThinkingContext>(),
                Arg.Any<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>())
            .Returns(Task.FromResult(turnResult));

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

        _turnManager
            .ProcessTurnAsync(
                Arg.Any<ThinkingContext>(),
                Arg.Any<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>())
            .Returns(Task.FromResult(turnResult));

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

        _turnManager
            .ProcessTurnAsync(
                Arg.Any<ThinkingContext>(),
                Arg.Any<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>())
            .Returns(callInfo =>
            {
                capturedContext = callInfo.ArgAt<ThinkingContext>(0);
                return Task.FromResult(turnResult);
            });

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

        _turnManager
            .ProcessTurnAsync(
                Arg.Any<ThinkingContext>(),
                Arg.Any<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>())
            .Returns(callInfo =>
            {
                capturedContext = callInfo.ArgAt<ThinkingContext>(0);
                return Task.FromResult(turnResult);
            });

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

        _turnManager
            .ProcessTurnAsync(
                Arg.Any<ThinkingContext>(),
                Arg.Any<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>())
            .Returns(callInfo =>
            {
                capturedContext = callInfo.ArgAt<ThinkingContext>(0);
                return Task.FromResult(turnResult);
            });

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
        var action = () => new ThinkingChatClient(null!, _turnManager);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullTurnManager_Throws()
    {
        // Act
        var action = () => new ThinkingChatClient(_innerClient, null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task GetStreamingResponseAsync_CollectsAndYieldsWithOrchestration()
    {
        // Arrange
        var messages = new List<ChatMessage> { new(ChatRole.User, "Test") };
        var updates = new List<ChatResponseUpdate>
        {
            new() { Contents = [new TextContent("Hello ")] },
            new() { Contents = [new TextContent("World")] }
        };

        _innerClient
            .GetStreamingResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(updates.ToAsyncEnumerable());

        var expectedResponse = new ChatResponse([new ChatMessage(ChatRole.Assistant, "Hello World")]);
        var turnResult = TurnResult.Success(expectedResponse, TurnMetrics.CreateBuilder().Build());

        _turnManager
            .ProcessTurnAsync(
                Arg.Any<ThinkingContext>(),
                Arg.Any<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>())
            .Returns(Task.FromResult(turnResult));

        // Act
        var result = new List<ChatResponseUpdate>();
        await foreach (var update in _client.GetStreamingResponseAsync(messages))
        {
            result.Add(update);
        }

        // Assert - should have original 2 chunks plus 1 metadata chunk
        result.Should().HaveCount(3);
        result[0].Text.Should().Be("Hello ");
        result[1].Text.Should().Be("World");

        // Last chunk should contain TurnResult metadata
        result[2].AdditionalProperties.Should().ContainKey(ThinkingChatClient.TurnResultKey);

        // TurnManager should have been called
        await _turnManager.Received(1).ProcessTurnAsync(
            Arg.Any<ThinkingContext>(),
            Arg.Any<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>());
    }

    [Fact]
    public async Task GetStreamingResponseAsync_NullMessages_Throws()
    {
        // Arrange
        var turnResult = TurnResult.Success(
            new ChatResponse([new ChatMessage(ChatRole.Assistant, "Hi")]),
            TurnMetrics.CreateBuilder().Build());

        _turnManager
            .ProcessTurnAsync(
                Arg.Any<ThinkingContext>(),
                Arg.Any<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>())
            .Returns(Task.FromResult(turnResult));

        // Act
        var action = async () =>
        {
            await foreach (var _ in _client.GetStreamingResponseAsync(null!)) { }
        };

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>();
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

    [Fact]
    public void WithSession_CreatesOptionsWithSessionId()
    {
        // Act
        var options = ThinkingChatClientExtensions.WithSession("test-session");

        // Assert
        options.Should().NotBeNull();
        options.AdditionalProperties.Should().ContainKey("IndexThinking.SessionId");
        options.AdditionalProperties!["IndexThinking.SessionId"].Should().Be("test-session");
    }

    [Fact]
    public void WithSessionId_AddsToExistingOptions()
    {
        // Arrange
        var existingOptions = new ChatOptions { ModelId = "gpt-4" };

        // Act
        var result = ThinkingChatClientExtensions.WithSessionId(existingOptions, "my-session");

        // Assert
        result.ModelId.Should().Be("gpt-4");
        result.AdditionalProperties.Should().ContainKey("IndexThinking.SessionId");
        result.AdditionalProperties!["IndexThinking.SessionId"].Should().Be("my-session");
    }

    [Fact]
    public void WithSessionId_NullOptions_CreatesNew()
    {
        // Act
        var result = ThinkingChatClientExtensions.WithSessionId(null, "my-session");

        // Assert
        result.Should().NotBeNull();
        result.AdditionalProperties!["IndexThinking.SessionId"].Should().Be("my-session");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void WithSession_NullOrEmptySessionId_Throws(string? sessionId)
    {
        // Act & Assert
        var action = () => ThinkingChatClientExtensions.WithSession(sessionId!);
        action.Should().Throw<ArgumentException>();
    }
}

public class ThinkingChatClientContextIntegrationTests
{
    private readonly IChatClient _innerClient;
    private readonly IThinkingTurnManager _turnManager;
    private readonly IContextTracker _contextTracker;
    private readonly IContextInjector _contextInjector;

    public ThinkingChatClientContextIntegrationTests()
    {
        _innerClient = Substitute.For<IChatClient>();
        _turnManager = Substitute.For<IThinkingTurnManager>();
        _contextTracker = Substitute.For<IContextTracker>();
        _contextInjector = Substitute.For<IContextInjector>();
    }

    [Fact]
    public async Task GetResponseAsync_WithContext_InjectsHistory()
    {
        // Arrange
        var messages = new List<ChatMessage> { new(ChatRole.User, "Current message") };
        var previousTurn = new ConversationTurn
        {
            UserMessage = new ChatMessage(ChatRole.User, "Previous"),
            AssistantResponse = new ChatResponse([new ChatMessage(ChatRole.Assistant, "Response")])
        };
        var context = new ConversationContext
        {
            SessionId = "test-session",
            RecentTurns = [previousTurn]
        };

        _contextTracker
            .GetContext("test-session")
            .Returns(context);

        var enrichedMessages = new List<ChatMessage>
        {
            new(ChatRole.User, "Previous"),
            new(ChatRole.Assistant, "Response"),
            new(ChatRole.User, "Current message")
        };

        _contextInjector
            .InjectContext(Arg.Any<IList<ChatMessage>>(), context)
            .Returns(enrichedMessages);

        var expectedResponse = new ChatResponse([new ChatMessage(ChatRole.Assistant, "Reply")]);
        var turnResult = TurnResult.Success(expectedResponse, TurnMetrics.CreateBuilder().Build());
        IList<ChatMessage>? capturedMessages = null;

        _turnManager
            .ProcessTurnAsync(
                Arg.Any<ThinkingContext>(),
                Arg.Any<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>())
            .Returns(callInfo =>
            {
                capturedMessages = callInfo.ArgAt<ThinkingContext>(0).Messages;
                return Task.FromResult(turnResult);
            });

        var options = new ThinkingChatClientOptions
        {
            EnableContextTracking = true,
            EnableContextInjection = true
        };

        var client = new ThinkingChatClient(
            _innerClient,
            _turnManager,
            options,
            _contextTracker,
            _contextInjector);

        var chatOptions = ThinkingChatClientExtensions.WithSession("test-session");

        // Act
        await client.GetResponseAsync(messages, chatOptions);

        // Assert
        capturedMessages.Should().HaveCount(3);
        _contextInjector.Received(1).InjectContext(Arg.Any<IList<ChatMessage>>(), context);
    }

    [Fact]
    public async Task GetResponseAsync_TracksConversation()
    {
        // Arrange
        var messages = new List<ChatMessage> { new(ChatRole.User, "Hello") };
        var expectedResponse = new ChatResponse([new ChatMessage(ChatRole.Assistant, "Hi")]);
        var turnResult = TurnResult.Success(expectedResponse, TurnMetrics.CreateBuilder().Build());

        _contextTracker
            .GetContext(Arg.Any<string>())
            .Returns(ConversationContext.Empty("test-session"));

        _turnManager
            .ProcessTurnAsync(
                Arg.Any<ThinkingContext>(),
                Arg.Any<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>())
            .Returns(Task.FromResult(turnResult));

        var options = new ThinkingChatClientOptions
        {
            EnableContextTracking = true,
            EnableContextInjection = true
        };

        var client = new ThinkingChatClient(
            _innerClient,
            _turnManager,
            options,
            _contextTracker,
            _contextInjector);

        var chatOptions = ThinkingChatClientExtensions.WithSession("test-session");

        // Act
        await client.GetResponseAsync(messages, chatOptions);

        // Assert
        _contextTracker.Received(1)
            .Track("test-session", Arg.Is<ChatMessage>(m => m.Text == "Hello"), expectedResponse);
    }

    [Fact]
    public async Task GetResponseAsync_DisabledContextTracking_DoesNotTrack()
    {
        // Arrange
        var messages = new List<ChatMessage> { new(ChatRole.User, "Hello") };
        var expectedResponse = new ChatResponse([new ChatMessage(ChatRole.Assistant, "Hi")]);
        var turnResult = TurnResult.Success(expectedResponse, TurnMetrics.CreateBuilder().Build());

        _turnManager
            .ProcessTurnAsync(
                Arg.Any<ThinkingContext>(),
                Arg.Any<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>())
            .Returns(Task.FromResult(turnResult));

        var options = new ThinkingChatClientOptions
        {
            EnableContextTracking = false,
            EnableContextInjection = false
        };

        var client = new ThinkingChatClient(
            _innerClient,
            _turnManager,
            options,
            _contextTracker,
            _contextInjector);

        // Act
        await client.GetResponseAsync(messages);

        // Assert
        _contextTracker.DidNotReceive().Track(Arg.Any<string>(), Arg.Any<ChatMessage>(), Arg.Any<ChatResponse>());
        _contextInjector.DidNotReceive().InjectContext(Arg.Any<IList<ChatMessage>>(), Arg.Any<ConversationContext>());
    }

    [Fact]
    public async Task GetResponseAsync_WithoutContextServices_WorksNormally()
    {
        // Arrange
        var messages = new List<ChatMessage> { new(ChatRole.User, "Hello") };
        var expectedResponse = new ChatResponse([new ChatMessage(ChatRole.Assistant, "Hi")]);
        var turnResult = TurnResult.Success(expectedResponse, TurnMetrics.CreateBuilder().Build());

        _turnManager
            .ProcessTurnAsync(
                Arg.Any<ThinkingContext>(),
                Arg.Any<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>())
            .Returns(Task.FromResult(turnResult));

        // No context tracker or injector
        var client = new ThinkingChatClient(
            _innerClient,
            _turnManager);

        // Act
        var response = await client.GetResponseAsync(messages);

        // Assert
        response.Should().NotBeNull();
    }
}

public class ThinkingChatClientOptionsContextTests
{
    [Fact]
    public void DefaultOptions_HasContextOptionsEnabled()
    {
        // Arrange & Act
        var options = new ThinkingChatClientOptions();

        // Assert
        options.EnableContextTracking.Should().BeTrue();
        options.EnableContextInjection.Should().BeTrue();
        options.MaxContextTurns.Should().Be(5);
        options.ContextTrackerOptions.Should().NotBeNull();
        options.ContextInjectorOptions.Should().NotBeNull();
    }

    [Fact]
    public void ContextOptions_CanBeCustomized()
    {
        // Arrange & Act
        var options = new ThinkingChatClientOptions
        {
            EnableContextTracking = false,
            EnableContextInjection = false,
            MaxContextTurns = 10,
            ContextTrackerOptions = new ContextTrackerOptions { MaxTurns = 20 },
            ContextInjectorOptions = new ContextInjectorOptions { MaxTurnsToInject = 3 }
        };

        // Assert
        options.EnableContextTracking.Should().BeFalse();
        options.EnableContextInjection.Should().BeFalse();
        options.MaxContextTurns.Should().Be(10);
        options.ContextTrackerOptions.MaxTurns.Should().Be(20);
        options.ContextInjectorOptions.MaxTurnsToInject.Should().Be(3);
    }
}
