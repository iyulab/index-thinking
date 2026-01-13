using FluentAssertions;
using IndexThinking.Core;
using IndexThinking.Stores;
using Xunit;

namespace IndexThinking.Tests.Stores;

public class InMemoryThinkingStateStoreTests
{
    private readonly InMemoryThinkingStateStore _store = new();

    [Fact]
    public async Task GetAsync_WhenNotExists_ShouldReturnNull()
    {
        // Act
        var result = await _store.GetAsync("non-existent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_ShouldStoreState()
    {
        // Arrange
        var state = new ThinkingState { SessionId = "session-1" };

        // Act
        await _store.SetAsync("session-1", state);
        var result = await _store.GetAsync("session-1");

        // Assert
        result.Should().NotBeNull();
        result!.SessionId.Should().Be("session-1");
    }

    [Fact]
    public async Task SetAsync_ShouldOverwriteExisting()
    {
        // Arrange
        var original = new ThinkingState
        {
            SessionId = "session-1",
            TotalThinkingTokens = 100
        };
        var updated = new ThinkingState
        {
            SessionId = "session-1",
            TotalThinkingTokens = 200
        };

        // Act
        await _store.SetAsync("session-1", original);
        await _store.SetAsync("session-1", updated);
        var result = await _store.GetAsync("session-1");

        // Assert
        result.Should().NotBeNull();
        result!.TotalThinkingTokens.Should().Be(200);
    }

    [Fact]
    public async Task RemoveAsync_ShouldDeleteState()
    {
        // Arrange
        var state = new ThinkingState { SessionId = "session-1" };
        await _store.SetAsync("session-1", state);

        // Act
        await _store.RemoveAsync("session-1");
        var result = await _store.GetAsync("session-1");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RemoveAsync_WhenNotExists_ShouldNotThrow()
    {
        // Act
        var action = () => _store.RemoveAsync("non-existent");

        // Assert
        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExistsAsync_WhenExists_ShouldReturnTrue()
    {
        // Arrange
        var state = new ThinkingState { SessionId = "session-1" };
        await _store.SetAsync("session-1", state);

        // Act
        var result = await _store.ExistsAsync("session-1");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WhenNotExists_ShouldReturnFalse()
    {
        // Act
        var result = await _store.ExistsAsync("non-existent");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task Count_ShouldReflectStoredItems()
    {
        // Arrange
        _store.Count.Should().Be(0);

        await _store.SetAsync("s1", new ThinkingState { SessionId = "s1" });
        await _store.SetAsync("s2", new ThinkingState { SessionId = "s2" });

        // Assert
        _store.Count.Should().Be(2);
    }

    [Fact]
    public async Task Clear_ShouldRemoveAllStates()
    {
        // Arrange
        await _store.SetAsync("s1", new ThinkingState { SessionId = "s1" });
        await _store.SetAsync("s2", new ThinkingState { SessionId = "s2" });

        // Act
        _store.Clear();

        // Assert
        _store.Count.Should().Be(0);
        (await _store.ExistsAsync("s1")).Should().BeFalse();
        (await _store.ExistsAsync("s2")).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAsync_WithInvalidSessionId_ShouldThrow(string? sessionId)
    {
        // Act
        var action = () => _store.GetAsync(sessionId!);

        // Assert
        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SetAsync_WithInvalidSessionId_ShouldThrow(string? sessionId)
    {
        // Arrange
        var state = new ThinkingState { SessionId = "valid" };

        // Act
        var action = () => _store.SetAsync(sessionId!, state);

        // Assert
        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SetAsync_WithNullState_ShouldThrow()
    {
        // Act
        var action = () => _store.SetAsync("session", null!);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ConcurrentAccess_ShouldBeThreadSafe()
    {
        // Arrange
        var tasks = new List<Task>();
        var sessionIds = Enumerable.Range(1, 100).Select(i => $"session-{i}").ToList();

        // Act - concurrent writes
        foreach (var sessionId in sessionIds)
        {
            tasks.Add(Task.Run(async () =>
            {
                await _store.SetAsync(sessionId, new ThinkingState { SessionId = sessionId });
            }));
        }
        await Task.WhenAll(tasks);

        // Assert
        _store.Count.Should().Be(100);

        // Verify all can be read
        foreach (var sessionId in sessionIds)
        {
            var result = await _store.GetAsync(sessionId);
            result.Should().NotBeNull();
            result!.SessionId.Should().Be(sessionId);
        }
    }
}
