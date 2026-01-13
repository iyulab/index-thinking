using FluentAssertions;
using IndexThinking.Core;
using Xunit;

namespace IndexThinking.Tests.Core;

public class ThinkingStateTests
{
    [Fact]
    public void ThinkingState_ShouldRequireSessionId()
    {
        // Arrange & Act
        var state = new ThinkingState { SessionId = "session-123" };

        // Assert
        state.SessionId.Should().Be("session-123");
        state.ModelId.Should().BeNull();
        state.ReasoningState.Should().BeNull();
        state.TotalThinkingTokens.Should().Be(0);
        state.TotalOutputTokens.Should().Be(0);
        state.ContinuationCount.Should().Be(0);
    }

    [Fact]
    public void ThinkingState_ShouldSetTimestamps()
    {
        // Arrange
        var before = DateTimeOffset.UtcNow;

        // Act
        var state = new ThinkingState { SessionId = "test" };

        // Assert
        var after = DateTimeOffset.UtcNow;
        state.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        state.UpdatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void ThinkingState_ShouldSupportFullConfiguration()
    {
        // Arrange
        var reasoningState = new ReasoningState
        {
            Provider = "anthropic",
            Data = new byte[] { 1, 2, 3 }
        };

        // Act
        var state = new ThinkingState
        {
            SessionId = "session-456",
            ModelId = "claude-3-opus",
            ReasoningState = reasoningState,
            TotalThinkingTokens = 5000,
            TotalOutputTokens = 2000,
            ContinuationCount = 3
        };

        // Assert
        state.SessionId.Should().Be("session-456");
        state.ModelId.Should().Be("claude-3-opus");
        state.ReasoningState.Should().Be(reasoningState);
        state.TotalThinkingTokens.Should().Be(5000);
        state.TotalOutputTokens.Should().Be(2000);
        state.ContinuationCount.Should().Be(3);
    }

    [Fact]
    public void ThinkingState_WithMethod_ShouldIncrementCounters()
    {
        // Arrange
        var original = new ThinkingState
        {
            SessionId = "session",
            TotalThinkingTokens = 1000,
            ContinuationCount = 1
        };

        // Act
        var updated = original with
        {
            TotalThinkingTokens = original.TotalThinkingTokens + 500,
            ContinuationCount = original.ContinuationCount + 1,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        updated.TotalThinkingTokens.Should().Be(1500);
        updated.ContinuationCount.Should().Be(2);
        original.TotalThinkingTokens.Should().Be(1000);
    }
}
