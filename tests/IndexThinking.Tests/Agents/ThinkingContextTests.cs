using IndexThinking.Agents;
using IndexThinking.Continuation;
using IndexThinking.Core;
using Microsoft.Extensions.AI;
using Xunit;

namespace IndexThinking.Tests.Agents;

public class ThinkingContextTests
{
    [Fact]
    public void Create_WithValidInputs_GeneratesTurnId()
    {
        // Arrange
        var sessionId = "test-session";
        var messages = new List<ChatMessage> { new(ChatRole.User, "Hello") };

        // Act
        var context = ThinkingContext.Create(sessionId, messages);

        // Assert
        Assert.NotNull(context.TurnId);
        Assert.Equal(32, context.TurnId.Length); // GUID without hyphens
        Assert.Equal(sessionId, context.SessionId);
        Assert.Same(messages, context.Messages);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidSessionId_Throws(string? sessionId)
    {
        // Arrange
        var messages = new List<ChatMessage> { new(ChatRole.User, "Hello") };

        // Act & Assert - ArgumentNullException is subclass of ArgumentException
        Assert.ThrowsAny<ArgumentException>(() => ThinkingContext.Create(sessionId!, messages));
    }

    [Fact]
    public void Create_WithNullMessages_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => ThinkingContext.Create("session", null!));
    }

    [Fact]
    public void Defaults_AreReasonable()
    {
        // Arrange & Act
        var context = ThinkingContext.Create("session", [new(ChatRole.User, "Test")]);

        // Assert
        Assert.NotNull(context.Budget);
        Assert.Equal(4096, context.Budget.ThinkingBudget);
        Assert.Equal(4096, context.Budget.AnswerBudget);
        Assert.NotNull(context.Continuation);
        Assert.Equal(5, context.Continuation.MaxContinuations);
        Assert.Null(context.EstimatedComplexity);
        Assert.Null(context.ModelId);
        Assert.True(context.StartedAt <= DateTimeOffset.UtcNow);
        Assert.Equal(CancellationToken.None, context.CancellationToken);
    }

    [Fact]
    public void WithComplexity_ReturnsNewInstance()
    {
        // Arrange
        var original = ThinkingContext.Create("session", [new(ChatRole.User, "Test")]);

        // Act
        var modified = original.WithComplexity(TaskComplexity.Complex);

        // Assert
        Assert.NotSame(original, modified);
        Assert.Null(original.EstimatedComplexity);
        Assert.Equal(TaskComplexity.Complex, modified.EstimatedComplexity);
        Assert.Equal(original.TurnId, modified.TurnId); // Other properties preserved
    }

    [Fact]
    public void WithModel_ReturnsNewInstance()
    {
        // Arrange
        var original = ThinkingContext.Create("session", [new(ChatRole.User, "Test")]);

        // Act
        var modified = original.WithModel("gpt-4o");

        // Assert
        Assert.NotSame(original, modified);
        Assert.Null(original.ModelId);
        Assert.Equal("gpt-4o", modified.ModelId);
    }

    [Fact]
    public void WithBudget_ReturnsNewInstance()
    {
        // Arrange
        var original = ThinkingContext.Create("session", [new(ChatRole.User, "Test")]);
        var newBudget = new BudgetConfig { ThinkingBudget = 8192, AnswerBudget = 2048 };

        // Act
        var modified = original.WithBudget(newBudget);

        // Assert
        Assert.NotSame(original, modified);
        Assert.Equal(4096, original.Budget.ThinkingBudget);
        Assert.Equal(8192, modified.Budget.ThinkingBudget);
        Assert.Equal(2048, modified.Budget.AnswerBudget);
    }

    [Fact]
    public void WithCancellation_ReturnsNewInstance()
    {
        // Arrange
        var original = ThinkingContext.Create("session", [new(ChatRole.User, "Test")]);
        using var cts = new CancellationTokenSource();

        // Act
        var modified = original.WithCancellation(cts.Token);

        // Assert
        Assert.NotSame(original, modified);
        Assert.Equal(CancellationToken.None, original.CancellationToken);
        Assert.Equal(cts.Token, modified.CancellationToken);
    }

    [Fact]
    public void Equality_WorksCorrectly()
    {
        // Arrange
        var messages = new List<ChatMessage> { new(ChatRole.User, "Test") };
        var now = DateTimeOffset.UtcNow;
        var context1 = new ThinkingContext
        {
            TurnId = "turn-1",
            SessionId = "session",
            Messages = messages,
            StartedAt = now
        };
        var context2 = new ThinkingContext
        {
            TurnId = "turn-1",
            SessionId = "session",
            Messages = messages,
            StartedAt = now
        };

        // Act & Assert
        Assert.Equal(context1, context2);
        Assert.Equal(context1.GetHashCode(), context2.GetHashCode());
    }
}
