using FluentAssertions;
using IndexThinking.Memory;
using Xunit;

namespace IndexThinking.Tests.Memory;

public class NullMemoryProviderTests
{
    [Fact]
    public void Instance_IsSingleton()
    {
        // Act
        var instance1 = NullMemoryProvider.Instance;
        var instance2 = NullMemoryProvider.Instance;

        // Assert
        instance1.Should().BeSameAs(instance2);
    }

    [Fact]
    public void IsConfigured_ReturnsFalse()
    {
        // Act & Assert
        NullMemoryProvider.Instance.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public async Task RecallAsync_ReturnsEmptyContext()
    {
        // Arrange
        var provider = NullMemoryProvider.Instance;

        // Act
        var result = await provider.RecallAsync("user-1", "session-1", "test query");

        // Assert
        result.Query.Should().Be("test query");
        result.HasMemories.Should().BeFalse();
        result.Memories.Should().BeEmpty();
        result.UserMemories.Should().BeEmpty();
        result.SessionMemories.Should().BeEmpty();
        result.TopicMemories.Should().BeEmpty();
    }

    [Fact]
    public async Task RecallAsync_WithNullSessionId_ReturnsEmptyContext()
    {
        // Arrange
        var provider = NullMemoryProvider.Instance;

        // Act
        var result = await provider.RecallAsync("user-1", null, "test query");

        // Assert
        result.HasMemories.Should().BeFalse();
    }

    [Fact]
    public async Task RecallAsync_NullUserId_Throws()
    {
        // Arrange
        var provider = NullMemoryProvider.Instance;

        // Act
        var action = () => provider.RecallAsync(null!, null, "query");

        // Assert
        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RecallAsync_EmptyUserId_Throws()
    {
        // Arrange
        var provider = NullMemoryProvider.Instance;

        // Act
        var action = () => provider.RecallAsync("", null, "query");

        // Assert
        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RecallAsync_NullQuery_Throws()
    {
        // Arrange
        var provider = NullMemoryProvider.Instance;

        // Act
        var action = () => provider.RecallAsync("user-1", null, null!);

        // Assert
        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RecallAsync_EmptyQuery_Throws()
    {
        // Arrange
        var provider = NullMemoryProvider.Instance;

        // Act
        var action = () => provider.RecallAsync("user-1", null, "");

        // Assert
        await action.Should().ThrowAsync<ArgumentException>();
    }
}
