using FluentAssertions;
using IndexThinking.Context;
using Microsoft.Extensions.AI;
using Xunit;

namespace IndexThinking.Tests.Context;

public class DefaultContextInjectorTests
{
    private readonly DefaultContextInjector _injector;

    public DefaultContextInjectorTests()
    {
        _injector = new DefaultContextInjector();
    }

    [Fact]
    public void InjectContext_NoHistory_ReturnsOriginalMessages()
    {
        // Arrange
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Current message")
        };
        var context = ConversationContext.Empty("session-1");

        // Act
        var result = _injector.InjectContext(messages, context);

        // Assert
        result.Should().HaveCount(1);
        result[0].Text.Should().Be("Current message");
    }

    [Fact]
    public void InjectContext_WithHistory_PrependsHistoryMessages()
    {
        // Arrange
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Current message")
        };
        var context = new ConversationContext
        {
            SessionId = "session-1",
            RecentTurns =
            [
                new ConversationTurn
                {
                    UserMessage = new ChatMessage(ChatRole.User, "Previous question"),
                    AssistantResponse = new ChatResponse([new ChatMessage(ChatRole.Assistant, "Previous answer")])
                }
            ]
        };

        // Act
        var result = _injector.InjectContext(messages, context);

        // Assert
        result.Should().HaveCount(3);
        result[0].Text.Should().Be("Previous question");
        result[0].Role.Should().Be(ChatRole.User);
        result[1].Text.Should().Be("Previous answer");
        result[1].Role.Should().Be(ChatRole.Assistant);
        result[2].Text.Should().Be("Current message");
        result[2].Role.Should().Be(ChatRole.User);
    }

    [Fact]
    public void InjectContext_MultipleTurns_PreservesOrder()
    {
        // Arrange
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Third question")
        };
        var context = new ConversationContext
        {
            SessionId = "session-1",
            RecentTurns =
            [
                new ConversationTurn
                {
                    UserMessage = new ChatMessage(ChatRole.User, "First question"),
                    AssistantResponse = new ChatResponse([new ChatMessage(ChatRole.Assistant, "First answer")])
                },
                new ConversationTurn
                {
                    UserMessage = new ChatMessage(ChatRole.User, "Second question"),
                    AssistantResponse = new ChatResponse([new ChatMessage(ChatRole.Assistant, "Second answer")])
                }
            ]
        };

        // Act
        var result = _injector.InjectContext(messages, context);

        // Assert
        result.Should().HaveCount(5);
        result[0].Text.Should().Be("First question");
        result[1].Text.Should().Be("First answer");
        result[2].Text.Should().Be("Second question");
        result[3].Text.Should().Be("Second answer");
        result[4].Text.Should().Be("Third question");
    }

    [Fact]
    public void InjectContext_TurnWithoutResponse_IncludesOnlyUserMessage()
    {
        // Arrange
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Current")
        };
        var context = new ConversationContext
        {
            SessionId = "session-1",
            RecentTurns =
            [
                new ConversationTurn
                {
                    UserMessage = new ChatMessage(ChatRole.User, "Previous"),
                    AssistantResponse = null
                }
            ]
        };

        // Act
        var result = _injector.InjectContext(messages, context);

        // Assert
        result.Should().HaveCount(2);
        result[0].Text.Should().Be("Previous");
        result[1].Text.Should().Be("Current");
    }

    [Fact]
    public void InjectContext_DisabledInjection_ReturnsOriginal()
    {
        // Arrange
        var injector = new DefaultContextInjector(new ContextInjectorOptions
        {
            EnableInjection = false
        });
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Current")
        };
        var context = new ConversationContext
        {
            SessionId = "session-1",
            RecentTurns =
            [
                new ConversationTurn
                {
                    UserMessage = new ChatMessage(ChatRole.User, "Previous"),
                    AssistantResponse = new ChatResponse([new ChatMessage(ChatRole.Assistant, "Answer")])
                }
            ]
        };

        // Act
        var result = injector.InjectContext(messages, context);

        // Assert
        result.Should().HaveCount(1);
        result[0].Text.Should().Be("Current");
    }

    [Fact]
    public void InjectContext_MaxTurnsLimit_OnlyIncludesRecentTurns()
    {
        // Arrange
        var injector = new DefaultContextInjector(new ContextInjectorOptions
        {
            MaxTurnsToInject = 1
        });
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Current")
        };
        var context = new ConversationContext
        {
            SessionId = "session-1",
            RecentTurns =
            [
                new ConversationTurn
                {
                    UserMessage = new ChatMessage(ChatRole.User, "Old"),
                    AssistantResponse = new ChatResponse([new ChatMessage(ChatRole.Assistant, "Old answer")])
                },
                new ConversationTurn
                {
                    UserMessage = new ChatMessage(ChatRole.User, "Recent"),
                    AssistantResponse = new ChatResponse([new ChatMessage(ChatRole.Assistant, "Recent answer")])
                }
            ]
        };

        // Act
        var result = injector.InjectContext(messages, context);

        // Assert
        result.Should().HaveCount(3); // Only most recent turn + current
        result[0].Text.Should().Be("Recent");
        result[1].Text.Should().Be("Recent answer");
        result[2].Text.Should().Be("Current");
    }

    [Fact]
    public void InjectContext_NullMessages_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _injector.InjectContext(null!, ConversationContext.Empty("s"));

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void InjectContext_NullContext_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _injector.InjectContext([], null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}

public class ContextInjectorOptionsTests
{
    [Fact]
    public void Default_HasReasonableValues()
    {
        // Arrange & Act
        var options = ContextInjectorOptions.Default;

        // Assert
        options.EnableInjection.Should().BeTrue();
        options.MaxTurnsToInject.Should().Be(5);
    }

    [Fact]
    public void CustomOptions_OverrideDefaults()
    {
        // Arrange & Act
        var options = new ContextInjectorOptions
        {
            EnableInjection = false,
            MaxTurnsToInject = 10
        };

        // Assert
        options.EnableInjection.Should().BeFalse();
        options.MaxTurnsToInject.Should().Be(10);
    }
}
