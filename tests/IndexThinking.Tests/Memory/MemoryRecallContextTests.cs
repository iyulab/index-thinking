using FluentAssertions;
using IndexThinking.Memory;
using Xunit;

namespace IndexThinking.Tests.Memory;

public class MemoryRecallContextTests
{
    [Fact]
    public void Empty_ReturnsEmptyContext()
    {
        // Act
        var context = MemoryRecallContext.Empty("test query");

        // Assert
        context.Query.Should().Be("test query");
        context.Memories.Should().BeEmpty();
        context.UserMemories.Should().BeEmpty();
        context.SessionMemories.Should().BeEmpty();
        context.TopicMemories.Should().BeEmpty();
        context.HasMemories.Should().BeFalse();
        context.TotalCount.Should().Be(0);
    }

    [Fact]
    public void HasMemories_WithMemories_ReturnsTrue()
    {
        // Arrange
        var context = new MemoryRecallContext
        {
            Query = "test",
            Memories = [new MemoryEntry { Content = "memory 1" }]
        };

        // Act & Assert
        context.HasMemories.Should().BeTrue();
        context.TotalCount.Should().Be(1);
    }

    [Fact]
    public void HasMemories_WithoutMemories_ReturnsFalse()
    {
        // Arrange
        var context = new MemoryRecallContext
        {
            Query = "test",
            Memories = []
        };

        // Act & Assert
        context.HasMemories.Should().BeFalse();
    }

    [Fact]
    public void MemoryEntry_WithAllProperties_PreservesValues()
    {
        // Arrange
        var storedAt = DateTimeOffset.UtcNow;
        var entry = new MemoryEntry
        {
            Content = "test content",
            Scope = "user",
            Relevance = 0.95f,
            StoredAt = storedAt
        };

        // Assert
        entry.Content.Should().Be("test content");
        entry.Scope.Should().Be("user");
        entry.Relevance.Should().Be(0.95f);
        entry.StoredAt.Should().Be(storedAt);
    }

    [Fact]
    public void RecalledAt_DefaultsToUtcNow()
    {
        // Arrange
        var before = DateTimeOffset.UtcNow;

        // Act
        var context = new MemoryRecallContext { Query = "test" };

        // Assert
        var after = DateTimeOffset.UtcNow;
        context.RecalledAt.Should().BeOnOrAfter(before);
        context.RecalledAt.Should().BeOnOrBefore(after);
    }
}
