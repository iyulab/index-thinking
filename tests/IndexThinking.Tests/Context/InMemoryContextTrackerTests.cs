using FluentAssertions;
using IndexThinking.Context;
using Microsoft.Extensions.AI;
using Xunit;

namespace IndexThinking.Tests.Context;

public class InMemoryContextTrackerTests : IDisposable
{
    private readonly InMemoryContextTracker _tracker;

    public InMemoryContextTrackerTests()
    {
        _tracker = new InMemoryContextTracker(new ContextTrackerOptions
        {
            MaxTurns = 3,
            SessionTtl = TimeSpan.FromHours(1),
            EnableCleanupTimer = false // Disable for testing
        });
    }

    public void Dispose()
    {
        _tracker.Dispose();
    }

    [Fact]
    public void Track_SingleTurn_StoresConversation()
    {
        // Arrange
        var sessionId = "session-1";
        var userMessage = new ChatMessage(ChatRole.User, "Hello");
        var response = new ChatResponse([new ChatMessage(ChatRole.Assistant, "Hi there!")]);

        // Act
        _tracker.Track(sessionId, userMessage, response);

        // Assert
        var context = _tracker.GetContext(sessionId);
        context.SessionId.Should().Be(sessionId);
        context.RecentTurns.Should().HaveCount(1);
        context.RecentTurns[0].UserMessage.Should().BeSameAs(userMessage);
        context.RecentTurns[0].AssistantResponse.Should().BeSameAs(response);
    }

    [Fact]
    public void Track_MultipleTurns_MaintainsOrder()
    {
        // Arrange
        var sessionId = "session-1";

        // Act
        _tracker.Track(sessionId, new ChatMessage(ChatRole.User, "First"));
        _tracker.Track(sessionId, new ChatMessage(ChatRole.User, "Second"));
        _tracker.Track(sessionId, new ChatMessage(ChatRole.User, "Third"));

        // Assert
        var context = _tracker.GetContext(sessionId);
        context.RecentTurns.Should().HaveCount(3);
        context.RecentTurns[0].UserText.Should().Be("First");
        context.RecentTurns[1].UserText.Should().Be("Second");
        context.RecentTurns[2].UserText.Should().Be("Third");
    }

    [Fact]
    public void Track_ExceedsMaxTurns_EvictsOldest()
    {
        // Arrange
        var sessionId = "session-1";

        // Act - Add 4 turns when max is 3
        _tracker.Track(sessionId, new ChatMessage(ChatRole.User, "First"));
        _tracker.Track(sessionId, new ChatMessage(ChatRole.User, "Second"));
        _tracker.Track(sessionId, new ChatMessage(ChatRole.User, "Third"));
        _tracker.Track(sessionId, new ChatMessage(ChatRole.User, "Fourth"));

        // Assert
        var context = _tracker.GetContext(sessionId);
        context.RecentTurns.Should().HaveCount(3);
        context.RecentTurns[0].UserText.Should().Be("Second"); // First was evicted
        context.RecentTurns[1].UserText.Should().Be("Third");
        context.RecentTurns[2].UserText.Should().Be("Fourth");
        context.TotalTurnCount.Should().Be(4); // Total count preserved
    }

    [Fact]
    public void GetContext_NoSession_ReturnsEmpty()
    {
        // Act
        var context = _tracker.GetContext("nonexistent");

        // Assert
        context.SessionId.Should().Be("nonexistent");
        context.RecentTurns.Should().BeEmpty();
        context.HasHistory.Should().BeFalse();
    }

    [Fact]
    public void HasContext_WithTurns_ReturnsTrue()
    {
        // Arrange
        var sessionId = "session-1";
        _tracker.Track(sessionId, new ChatMessage(ChatRole.User, "Hello"));

        // Act & Assert
        _tracker.HasContext(sessionId).Should().BeTrue();
    }

    [Fact]
    public void HasContext_NoSession_ReturnsFalse()
    {
        // Act & Assert
        _tracker.HasContext("nonexistent").Should().BeFalse();
    }

    [Fact]
    public void Clear_RemovesSession()
    {
        // Arrange
        var sessionId = "session-1";
        _tracker.Track(sessionId, new ChatMessage(ChatRole.User, "Hello"));

        // Act
        _tracker.Clear(sessionId);

        // Assert
        _tracker.HasContext(sessionId).Should().BeFalse();
        _tracker.GetContext(sessionId).RecentTurns.Should().BeEmpty();
    }

    [Fact]
    public void SessionCount_TracksActiveSessions()
    {
        // Act
        _tracker.Track("session-1", new ChatMessage(ChatRole.User, "A"));
        _tracker.Track("session-2", new ChatMessage(ChatRole.User, "B"));
        _tracker.Track("session-3", new ChatMessage(ChatRole.User, "C"));

        // Assert
        _tracker.SessionCount.Should().Be(3);
    }

    [Fact]
    public void MultipleSessions_AreIsolated()
    {
        // Arrange & Act
        _tracker.Track("session-1", new ChatMessage(ChatRole.User, "Message for session 1"));
        _tracker.Track("session-2", new ChatMessage(ChatRole.User, "Message for session 2"));

        // Assert
        var context1 = _tracker.GetContext("session-1");
        var context2 = _tracker.GetContext("session-2");

        context1.RecentTurns.Should().HaveCount(1);
        context1.RecentTurns[0].UserText.Should().Be("Message for session 1");

        context2.RecentTurns.Should().HaveCount(1);
        context2.RecentTurns[0].UserText.Should().Be("Message for session 2");
    }

    [Fact]
    public void Track_WithNullResponse_StoresUserMessageOnly()
    {
        // Arrange
        var sessionId = "session-1";
        var userMessage = new ChatMessage(ChatRole.User, "Hello");

        // Act
        _tracker.Track(sessionId, userMessage, response: null);

        // Assert
        var context = _tracker.GetContext(sessionId);
        context.RecentTurns[0].UserMessage.Should().BeSameAs(userMessage);
        context.RecentTurns[0].AssistantResponse.Should().BeNull();
    }

    [Fact]
    public void Track_NullSessionId_ThrowsArgumentException()
    {
        // Act
        var act = () => _tracker.Track(null!, new ChatMessage(ChatRole.User, "Hello"));

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Track_NullUserMessage_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _tracker.Track("session-1", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}

public class InMemoryContextTrackerExpirationTests
{
    [Fact]
    public void GetContext_ExpiredSession_ReturnsEmpty()
    {
        // Arrange
        var options = new ContextTrackerOptions
        {
            SessionTtl = TimeSpan.FromMilliseconds(50),
            EnableCleanupTimer = false
        };
        using var tracker = new InMemoryContextTracker(options);
        tracker.Track("session-1", new ChatMessage(ChatRole.User, "Hello"));

        // Act - Wait for expiration
        Thread.Sleep(100);
        var context = tracker.GetContext("session-1");

        // Assert
        context.RecentTurns.Should().BeEmpty();
        context.HasHistory.Should().BeFalse();
    }

    [Fact]
    public void HasContext_ExpiredSession_ReturnsFalse()
    {
        // Arrange
        var options = new ContextTrackerOptions
        {
            SessionTtl = TimeSpan.FromMilliseconds(50),
            EnableCleanupTimer = false
        };
        using var tracker = new InMemoryContextTracker(options);
        tracker.Track("session-1", new ChatMessage(ChatRole.User, "Hello"));

        // Act - Wait for expiration
        Thread.Sleep(100);

        // Assert
        tracker.HasContext("session-1").Should().BeFalse();
    }
}

public class ContextTrackerOptionsTests
{
    [Fact]
    public void Default_HasReasonableValues()
    {
        // Arrange & Act
        var options = ContextTrackerOptions.Default;

        // Assert
        options.MaxTurns.Should().Be(10);
        options.SessionTtl.Should().Be(TimeSpan.FromHours(1));
        options.EnableCleanupTimer.Should().BeTrue();
        options.CleanupInterval.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void CustomOptions_OverrideDefaults()
    {
        // Arrange & Act
        var options = new ContextTrackerOptions
        {
            MaxTurns = 20,
            SessionTtl = TimeSpan.FromMinutes(30),
            EnableCleanupTimer = false,
            CleanupInterval = TimeSpan.FromMinutes(10)
        };

        // Assert
        options.MaxTurns.Should().Be(20);
        options.SessionTtl.Should().Be(TimeSpan.FromMinutes(30));
        options.EnableCleanupTimer.Should().BeFalse();
        options.CleanupInterval.Should().Be(TimeSpan.FromMinutes(10));
    }
}
