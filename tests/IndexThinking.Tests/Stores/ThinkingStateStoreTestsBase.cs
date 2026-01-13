using FluentAssertions;
using IndexThinking.Abstractions;
using IndexThinking.Core;
using Xunit;

namespace IndexThinking.Tests.Stores;

/// <summary>
/// Abstract base test class for <see cref="IThinkingStateStore"/> implementations.
/// Provides common test cases that all implementations must pass.
/// </summary>
public abstract class ThinkingStateStoreTestsBase<TStore> : IAsyncLifetime
    where TStore : IThinkingStateStore
{
    protected TStore Store { get; private set; } = default!;

    protected abstract TStore CreateStore();

    public virtual Task InitializeAsync()
    {
        Store = CreateStore();
        return Task.CompletedTask;
    }

    public virtual Task DisposeAsync()
    {
        if (Store is IDisposable disposable)
        {
            disposable.Dispose();
        }
        return Task.CompletedTask;
    }

    #region Common Tests

    [Fact]
    public async Task GetAsync_WhenNotExists_ShouldReturnNull()
    {
        // Act
        var result = await Store.GetAsync("non-existent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_ShouldStoreState()
    {
        // Arrange
        var state = CreateTestState("session-1");

        // Act
        await Store.SetAsync("session-1", state);
        var result = await Store.GetAsync("session-1");

        // Assert
        result.Should().NotBeNull();
        result!.SessionId.Should().Be("session-1");
    }

    [Fact]
    public async Task SetAsync_ShouldOverwriteExisting()
    {
        // Arrange
        var original = CreateTestState("session-1", thinkingTokens: 100);
        var updated = CreateTestState("session-1", thinkingTokens: 200);

        // Act
        await Store.SetAsync("session-1", original);
        await Store.SetAsync("session-1", updated);
        var result = await Store.GetAsync("session-1");

        // Assert
        result.Should().NotBeNull();
        result!.TotalThinkingTokens.Should().Be(200);
    }

    [Fact]
    public async Task RemoveAsync_ShouldDeleteState()
    {
        // Arrange
        var state = CreateTestState("session-1");
        await Store.SetAsync("session-1", state);

        // Act
        await Store.RemoveAsync("session-1");
        var result = await Store.GetAsync("session-1");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RemoveAsync_WhenNotExists_ShouldNotThrow()
    {
        // Act
        var action = () => Store.RemoveAsync("non-existent");

        // Assert
        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExistsAsync_WhenExists_ShouldReturnTrue()
    {
        // Arrange
        var state = CreateTestState("session-1");
        await Store.SetAsync("session-1", state);

        // Act
        var result = await Store.ExistsAsync("session-1");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WhenNotExists_ShouldReturnFalse()
    {
        // Act
        var result = await Store.ExistsAsync("non-existent");

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAsync_WithInvalidSessionId_ShouldThrow(string? sessionId)
    {
        // Act
        var action = () => Store.GetAsync(sessionId!);

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
        var state = CreateTestState("valid");

        // Act
        var action = () => Store.SetAsync(sessionId!, state);

        // Assert
        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SetAsync_WithNullState_ShouldThrow()
    {
        // Act
        var action = () => Store.SetAsync("session", null!);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SetAsync_WithReasoningState_ShouldPreserveData()
    {
        // Arrange
        var reasoningState = new ReasoningState
        {
            Provider = "openai",
            Data = [1, 2, 3, 4, 5],
            CapturedAt = DateTimeOffset.UtcNow
        };

        var state = new ThinkingState
        {
            SessionId = "session-with-reasoning",
            ModelId = "gpt-4o",
            ReasoningState = reasoningState,
            TotalThinkingTokens = 500,
            TotalOutputTokens = 100,
            ContinuationCount = 2
        };

        // Act
        await Store.SetAsync("session-with-reasoning", state);
        var result = await Store.GetAsync("session-with-reasoning");

        // Assert
        result.Should().NotBeNull();
        result!.ReasoningState.Should().NotBeNull();
        result.ReasoningState!.Provider.Should().Be("openai");
        result.ReasoningState.Data.Should().BeEquivalentTo(new byte[] { 1, 2, 3, 4, 5 });
        result.ModelId.Should().Be("gpt-4o");
        result.TotalThinkingTokens.Should().Be(500);
        result.TotalOutputTokens.Should().Be(100);
        result.ContinuationCount.Should().Be(2);
    }

    [Fact]
    public async Task MultipleSessionIds_ShouldBeIndependent()
    {
        // Arrange
        var state1 = CreateTestState("session-1", thinkingTokens: 100);
        var state2 = CreateTestState("session-2", thinkingTokens: 200);
        var state3 = CreateTestState("session-3", thinkingTokens: 300);

        // Act
        await Store.SetAsync("session-1", state1);
        await Store.SetAsync("session-2", state2);
        await Store.SetAsync("session-3", state3);

        await Store.RemoveAsync("session-2");

        // Assert
        (await Store.GetAsync("session-1"))!.TotalThinkingTokens.Should().Be(100);
        (await Store.GetAsync("session-2")).Should().BeNull();
        (await Store.GetAsync("session-3"))!.TotalThinkingTokens.Should().Be(300);
    }

    #endregion

    #region Helper Methods

    protected static ThinkingState CreateTestState(
        string sessionId,
        string? modelId = null,
        int thinkingTokens = 0,
        int outputTokens = 0,
        int continuationCount = 0)
    {
        return new ThinkingState
        {
            SessionId = sessionId,
            ModelId = modelId,
            TotalThinkingTokens = thinkingTokens,
            TotalOutputTokens = outputTokens,
            ContinuationCount = continuationCount,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    #endregion
}
