using FluentAssertions;
using IndexThinking.Abstractions;
using IndexThinking.Context;
using IndexThinking.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IndexThinking.IntegrationTests;

/// <summary>
/// Integration tests for conversation context tracking and injection.
/// </summary>
public class ContextTrackingIntegrationTests
{
    #region Context Tracker Tests

    [Fact]
    public void ContextTracker_TrackMultipleTurns_PreservesHistory()
    {
        // Arrange
        var tracker = new InMemoryContextTracker();

        // Act
        tracker.Track("session-1",
            new ChatMessage(ChatRole.User, "Hello"),
            new ChatResponse([new ChatMessage(ChatRole.Assistant, "Hi there!")]));

        tracker.Track("session-1",
            new ChatMessage(ChatRole.User, "How are you?"),
            new ChatResponse([new ChatMessage(ChatRole.Assistant, "I'm doing well!")]));

        tracker.Track("session-1",
            new ChatMessage(ChatRole.User, "That's great"),
            new ChatResponse([new ChatMessage(ChatRole.Assistant, "Thanks!")]));

        var context = tracker.GetContext("session-1");

        // Assert
        context.RecentTurns.Should().HaveCount(3);
        context.RecentTurns[0].UserMessage.Text.Should().Be("Hello");
        context.RecentTurns[1].UserMessage.Text.Should().Be("How are you?");
        context.RecentTurns[2].UserMessage.Text.Should().Be("That's great");
    }

    [Fact]
    public void ContextTracker_SlidingWindow_RemovesOldTurns()
    {
        // Arrange
        var options = new ContextTrackerOptions { MaxTurns = 2 };
        var tracker = new InMemoryContextTracker(options);

        // Act - Add 3 turns, window size is 2
        tracker.Track("session-1",
            new ChatMessage(ChatRole.User, "First"),
            new ChatResponse([new ChatMessage(ChatRole.Assistant, "R1")]));

        tracker.Track("session-1",
            new ChatMessage(ChatRole.User, "Second"),
            new ChatResponse([new ChatMessage(ChatRole.Assistant, "R2")]));

        tracker.Track("session-1",
            new ChatMessage(ChatRole.User, "Third"),
            new ChatResponse([new ChatMessage(ChatRole.Assistant, "R3")]));

        var context = tracker.GetContext("session-1");

        // Assert
        context.RecentTurns.Should().HaveCount(2);
        context.RecentTurns[0].UserMessage.Text.Should().Be("Second");
        context.RecentTurns[1].UserMessage.Text.Should().Be("Third");
        context.TotalTurnCount.Should().Be(3);
    }

    [Fact]
    public void ContextTracker_MultipleSessions_Isolated()
    {
        // Arrange
        var tracker = new InMemoryContextTracker();

        // Act
        tracker.Track("session-A",
            new ChatMessage(ChatRole.User, "Message A"),
            new ChatResponse([new ChatMessage(ChatRole.Assistant, "Reply A")]));

        tracker.Track("session-B",
            new ChatMessage(ChatRole.User, "Message B"),
            new ChatResponse([new ChatMessage(ChatRole.Assistant, "Reply B")]));

        var contextA = tracker.GetContext("session-A");
        var contextB = tracker.GetContext("session-B");

        // Assert
        contextA.RecentTurns.Should().HaveCount(1);
        contextA.RecentTurns[0].UserMessage.Text.Should().Be("Message A");

        contextB.RecentTurns.Should().HaveCount(1);
        contextB.RecentTurns[0].UserMessage.Text.Should().Be("Message B");
    }

    [Fact]
    public void ContextTracker_Clear_RemovesSession()
    {
        // Arrange
        var tracker = new InMemoryContextTracker();
        tracker.Track("session-clear",
            new ChatMessage(ChatRole.User, "Hello"),
            new ChatResponse([new ChatMessage(ChatRole.Assistant, "Hi")]));

        // Act
        tracker.Clear("session-clear");
        var context = tracker.GetContext("session-clear");

        // Assert
        context.RecentTurns.Should().BeEmpty();
    }

    [Fact]
    public void ContextTracker_HasContext_ReturnsCorrectly()
    {
        // Arrange
        var tracker = new InMemoryContextTracker();

        // Assert - Before
        tracker.HasContext("new-session").Should().BeFalse();

        // Act
        tracker.Track("new-session",
            new ChatMessage(ChatRole.User, "Hello"),
            new ChatResponse([new ChatMessage(ChatRole.Assistant, "Hi")]));

        // Assert - After
        tracker.HasContext("new-session").Should().BeTrue();
    }

    [Fact]
    public void ContextTracker_EmptySession_ReturnsEmptyContext()
    {
        // Arrange
        var tracker = new InMemoryContextTracker();

        // Act
        var context = tracker.GetContext("non-existent");

        // Assert
        context.Should().NotBeNull();
        context.RecentTurns.Should().BeEmpty();
        context.HasHistory.Should().BeFalse();
    }

    #endregion

    #region Context Injector Tests

    [Fact]
    public void ContextInjector_InjectHistory_PrependsMessages()
    {
        // Arrange
        var injector = new DefaultContextInjector();
        var currentMessages = new List<ChatMessage>
        {
            new(ChatRole.User, "Current message")
        };

        var previousTurns = new List<ConversationTurn>
        {
            new()
            {
                UserMessage = new ChatMessage(ChatRole.User, "Previous 1"),
                AssistantResponse = new ChatResponse([new ChatMessage(ChatRole.Assistant, "Reply 1")])
            },
            new()
            {
                UserMessage = new ChatMessage(ChatRole.User, "Previous 2"),
                AssistantResponse = new ChatResponse([new ChatMessage(ChatRole.Assistant, "Reply 2")])
            }
        };

        var context = new ConversationContext
        {
            SessionId = "test",
            RecentTurns = previousTurns
        };

        // Act
        var result = injector.InjectContext(currentMessages, context);

        // Assert
        result.Should().HaveCount(5); // 2 turns (4 messages) + 1 current
        result[0].Role.Should().Be(ChatRole.User);
        result[0].Text.Should().Be("Previous 1");
        result[1].Role.Should().Be(ChatRole.Assistant);
        result[1].Text.Should().Be("Reply 1");
        result[4].Text.Should().Be("Current message");
    }

    [Fact]
    public void ContextInjector_WithMaxTurns_LimitsInjection()
    {
        // Arrange
        var options = new ContextInjectorOptions { MaxTurnsToInject = 1 };
        var injector = new DefaultContextInjector(options);

        var currentMessages = new List<ChatMessage>
        {
            new(ChatRole.User, "Current")
        };

        var previousTurns = new List<ConversationTurn>
        {
            new()
            {
                UserMessage = new ChatMessage(ChatRole.User, "Old"),
                AssistantResponse = new ChatResponse([new ChatMessage(ChatRole.Assistant, "Old Reply")])
            },
            new()
            {
                UserMessage = new ChatMessage(ChatRole.User, "Recent"),
                AssistantResponse = new ChatResponse([new ChatMessage(ChatRole.Assistant, "Recent Reply")])
            }
        };

        var context = new ConversationContext
        {
            SessionId = "test",
            RecentTurns = previousTurns
        };

        // Act
        var result = injector.InjectContext(currentMessages, context);

        // Assert - Only 1 turn (2 messages) + current
        result.Should().HaveCount(3);
        result[0].Text.Should().Be("Recent"); // Most recent turn
    }

    [Fact]
    public void ContextInjector_EmptyContext_ReturnsOriginalMessages()
    {
        // Arrange
        var injector = new DefaultContextInjector();
        var currentMessages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello")
        };

        var context = ConversationContext.Empty("test");

        // Act
        var result = injector.InjectContext(currentMessages, context);

        // Assert
        result.Should().HaveCount(1);
        result[0].Text.Should().Be("Hello");
    }

    [Fact]
    public void ContextInjector_DisabledInjection_ReturnsOriginal()
    {
        // Arrange
        var options = new ContextInjectorOptions { EnableInjection = false };
        var injector = new DefaultContextInjector(options);

        var currentMessages = new List<ChatMessage>
        {
            new(ChatRole.User, "Current")
        };

        var context = new ConversationContext
        {
            SessionId = "test",
            RecentTurns =
            [
                new()
                {
                    UserMessage = new ChatMessage(ChatRole.User, "Previous"),
                    AssistantResponse = new ChatResponse([new ChatMessage(ChatRole.Assistant, "Reply")])
                }
            ]
        };

        // Act
        var result = injector.InjectContext(currentMessages, context);

        // Assert
        result.Should().HaveCount(1);
        result[0].Text.Should().Be("Current");
    }

    #endregion

    #region DI Integration Tests

    [Fact]
    public void ContextServices_FromDI_ResolveCorrectly()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddIndexThinkingContext()
            .BuildServiceProvider();

        // Act
        var tracker = services.GetService<IContextTracker>();
        var injector = services.GetService<IContextInjector>();

        // Assert
        tracker.Should().NotBeNull();
        tracker.Should().BeOfType<InMemoryContextTracker>();

        injector.Should().NotBeNull();
        injector.Should().BeOfType<DefaultContextInjector>();
    }

    [Fact]
    public void ContextServices_WithOptions_ApplyConfiguration()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddIndexThinkingContext(
                new ContextTrackerOptions { MaxTurns = 50 },
                new ContextInjectorOptions { MaxTurnsToInject = 10 })
            .BuildServiceProvider();

        // Act - Use the services
        var tracker = services.GetRequiredService<IContextTracker>();

        // Add 51 turns
        for (int i = 0; i < 51; i++)
        {
            tracker.Track("test",
                new ChatMessage(ChatRole.User, $"Message {i}"),
                new ChatResponse([new ChatMessage(ChatRole.Assistant, $"Reply {i}")]));
        }

        var context = tracker.GetContext("test");

        // Assert - Should only have 50 (MaxTurns)
        context.RecentTurns.Should().HaveCount(50);
    }

    #endregion

    #region End-to-End Context Flow Tests

    [Fact]
    public void EndToEnd_TrackAndInject_ProducesCorrectMessages()
    {
        // Arrange
        var tracker = new InMemoryContextTracker();
        var injector = new DefaultContextInjector();

        // Simulate conversation
        tracker.Track("e2e-session",
            new ChatMessage(ChatRole.User, "What is 2+2?"),
            new ChatResponse([new ChatMessage(ChatRole.Assistant, "4")]));

        tracker.Track("e2e-session",
            new ChatMessage(ChatRole.User, "And 3+3?"),
            new ChatResponse([new ChatMessage(ChatRole.Assistant, "6")]));

        // New user message
        var newMessage = new List<ChatMessage>
        {
            new(ChatRole.User, "What about 4+4?")
        };

        // Act
        var context = tracker.GetContext("e2e-session");
        var enrichedMessages = injector.InjectContext(newMessage, context);

        // Assert
        enrichedMessages.Should().HaveCount(5);
        enrichedMessages[0].Text.Should().Be("What is 2+2?");
        enrichedMessages[1].Text.Should().Be("4");
        enrichedMessages[2].Text.Should().Be("And 3+3?");
        enrichedMessages[3].Text.Should().Be("6");
        enrichedMessages[4].Text.Should().Be("What about 4+4?");
    }

    #endregion
}
