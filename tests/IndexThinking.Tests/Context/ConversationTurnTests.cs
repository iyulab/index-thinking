using FluentAssertions;
using IndexThinking.Context;
using Microsoft.Extensions.AI;
using Xunit;

namespace IndexThinking.Tests.Context;

public class ConversationTurnTests
{
    [Fact]
    public void ConversationTurn_WithAllProperties_PreservesValues()
    {
        // Arrange
        var userMessage = new ChatMessage(ChatRole.User, "Hello");
        var response = new ChatResponse([new ChatMessage(ChatRole.Assistant, "Hi there!")]);
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var turn = new ConversationTurn
        {
            UserMessage = userMessage,
            AssistantResponse = response,
            Timestamp = timestamp
        };

        // Assert
        turn.UserMessage.Should().BeSameAs(userMessage);
        turn.AssistantResponse.Should().BeSameAs(response);
        turn.Timestamp.Should().Be(timestamp);
        turn.TurnId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void UserText_ReturnsUserMessageText()
    {
        // Arrange
        var turn = new ConversationTurn
        {
            UserMessage = new ChatMessage(ChatRole.User, "Test message")
        };

        // Assert
        turn.UserText.Should().Be("Test message");
    }

    [Fact]
    public void AssistantText_WithResponse_ReturnsFirstMessageText()
    {
        // Arrange
        var turn = new ConversationTurn
        {
            UserMessage = new ChatMessage(ChatRole.User, "Hello"),
            AssistantResponse = new ChatResponse([new ChatMessage(ChatRole.Assistant, "Response text")])
        };

        // Assert
        turn.AssistantText.Should().Be("Response text");
    }

    [Fact]
    public void AssistantText_WithoutResponse_ReturnsNull()
    {
        // Arrange
        var turn = new ConversationTurn
        {
            UserMessage = new ChatMessage(ChatRole.User, "Hello"),
            AssistantResponse = null
        };

        // Assert
        turn.AssistantText.Should().BeNull();
    }

    [Fact]
    public void TurnId_IsUniquePerInstance()
    {
        // Arrange & Act
        var turn1 = new ConversationTurn { UserMessage = new ChatMessage(ChatRole.User, "A") };
        var turn2 = new ConversationTurn { UserMessage = new ChatMessage(ChatRole.User, "B") };

        // Assert
        turn1.TurnId.Should().NotBe(turn2.TurnId);
    }
}

public class ConversationContextTests
{
    [Fact]
    public void Empty_ReturnsEmptyContext()
    {
        // Act
        var context = ConversationContext.Empty("session-1");

        // Assert
        context.SessionId.Should().Be("session-1");
        context.RecentTurns.Should().BeEmpty();
        context.TotalTurnCount.Should().Be(0);
        context.HasHistory.Should().BeFalse();
        context.WindowSize.Should().Be(0);
    }

    [Fact]
    public void HasHistory_WithTurns_ReturnsTrue()
    {
        // Arrange
        var context = new ConversationContext
        {
            SessionId = "session-1",
            RecentTurns = [new ConversationTurn { UserMessage = new ChatMessage(ChatRole.User, "Hi") }]
        };

        // Assert
        context.HasHistory.Should().BeTrue();
    }

    [Fact]
    public void WindowSize_ReturnsRecentTurnsCount()
    {
        // Arrange
        var context = new ConversationContext
        {
            SessionId = "session-1",
            RecentTurns =
            [
                new ConversationTurn { UserMessage = new ChatMessage(ChatRole.User, "1") },
                new ConversationTurn { UserMessage = new ChatMessage(ChatRole.User, "2") },
                new ConversationTurn { UserMessage = new ChatMessage(ChatRole.User, "3") }
            ]
        };

        // Assert
        context.WindowSize.Should().Be(3);
    }

    [Fact]
    public void TotalTurnCount_CanExceedWindowSize()
    {
        // Arrange
        var context = new ConversationContext
        {
            SessionId = "session-1",
            RecentTurns = [new ConversationTurn { UserMessage = new ChatMessage(ChatRole.User, "Latest") }],
            TotalTurnCount = 100 // Many turns occurred but only 1 in window
        };

        // Assert
        context.WindowSize.Should().Be(1);
        context.TotalTurnCount.Should().Be(100);
    }
}
