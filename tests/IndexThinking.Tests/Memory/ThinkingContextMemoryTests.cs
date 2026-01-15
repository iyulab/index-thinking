using FluentAssertions;
using IndexThinking.Agents;
using IndexThinking.Memory;
using Microsoft.Extensions.AI;
using Xunit;

namespace IndexThinking.Tests.Memory;

public class ThinkingContextMemoryTests
{
    [Fact]
    public void WithUserId_SetsUserId()
    {
        // Arrange
        var context = CreateTestContext();

        // Act
        var updated = context.WithUserId("user-123");

        // Assert
        updated.UserId.Should().Be("user-123");
        updated.SessionId.Should().Be(context.SessionId); // unchanged
    }

    [Fact]
    public void WithMemory_SetsMemoryContext()
    {
        // Arrange
        var context = CreateTestContext();
        var memoryContext = new MemoryRecallContext
        {
            Query = "test",
            Memories = [new MemoryEntry { Content = "memory 1" }]
        };

        // Act
        var updated = context.WithMemory(memoryContext);

        // Assert
        updated.MemoryContext.Should().BeSameAs(memoryContext);
        updated.HasMemory.Should().BeTrue();
    }

    [Fact]
    public void HasMemory_WithNullContext_ReturnsFalse()
    {
        // Arrange
        var context = CreateTestContext();

        // Assert
        context.MemoryContext.Should().BeNull();
        context.HasMemory.Should().BeFalse();
    }

    [Fact]
    public void HasMemory_WithEmptyContext_ReturnsFalse()
    {
        // Arrange
        var memoryContext = MemoryRecallContext.Empty("test");
        var context = CreateTestContext().WithMemory(memoryContext);

        // Assert
        context.HasMemory.Should().BeFalse();
    }

    [Fact]
    public void HasMemory_WithMemories_ReturnsTrue()
    {
        // Arrange
        var memoryContext = new MemoryRecallContext
        {
            Query = "test",
            Memories = [new MemoryEntry { Content = "some memory" }]
        };
        var context = CreateTestContext().WithMemory(memoryContext);

        // Assert
        context.HasMemory.Should().BeTrue();
    }

    [Fact]
    public void UserId_DefaultsToNull()
    {
        // Arrange
        var context = CreateTestContext();

        // Assert
        context.UserId.Should().BeNull();
    }

    [Fact]
    public void MemoryContext_DefaultsToNull()
    {
        // Arrange
        var context = CreateTestContext();

        // Assert
        context.MemoryContext.Should().BeNull();
    }

    private static ThinkingContext CreateTestContext()
    {
        return new ThinkingContext
        {
            TurnId = "turn-1",
            SessionId = "session-1",
            Messages = [new ChatMessage(ChatRole.User, "Hello")]
        };
    }
}
