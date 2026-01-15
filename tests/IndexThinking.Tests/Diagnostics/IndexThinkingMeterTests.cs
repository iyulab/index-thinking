using FluentAssertions;
using IndexThinking.Agents;
using IndexThinking.Core;
using IndexThinking.Diagnostics;
using Microsoft.Extensions.AI;
using Xunit;

namespace IndexThinking.Tests.Diagnostics;

/// <summary>
/// Tests for <see cref="IndexThinkingMeter"/>.
/// </summary>
public class IndexThinkingMeterTests
{
    [Fact]
    public void MeterName_ShouldBeIndexThinking()
    {
        // Assert
        IndexThinkingMeter.MeterName.Should().Be("IndexThinking");
    }

    [Fact]
    public void Constructor_WithoutFactory_ShouldNotThrow()
    {
        // Act
        var action = () => new IndexThinkingMeter();

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithNullFactory_ShouldNotThrow()
    {
        // Act
        var action = () => new IndexThinkingMeter(null);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void RecordTurn_WithValidTurnResult_ShouldNotThrow()
    {
        // Arrange
        using var meter = new IndexThinkingMeter();
        var turnResult = CreateTurnResult();

        // Act
        var action = () => meter.RecordTurn(turnResult);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void RecordTurn_WithSessionId_ShouldNotThrow()
    {
        // Arrange
        using var meter = new IndexThinkingMeter();
        var turnResult = CreateTurnResult();

        // Act
        var action = () => meter.RecordTurn(turnResult, "test-session");

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void RecordTurn_WithNullTurnResult_ShouldThrow()
    {
        // Arrange
        using var meter = new IndexThinkingMeter();

        // Act
        var action = () => meter.RecordTurn((TurnResult)null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("turnResult");
    }

    [Fact]
    public void RecordTurn_WithMetrics_ShouldNotThrow()
    {
        // Arrange
        using var meter = new IndexThinkingMeter();
        var metrics = new TurnMetrics
        {
            ThinkingTokens = 100,
            ContinuationCount = 1,
            Duration = TimeSpan.FromSeconds(2),
            DetectedComplexity = TaskComplexity.Moderate
        };

        // Act
        var action = () => meter.RecordTurn(metrics);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void RecordTurn_WithMetricsAndAllOptions_ShouldNotThrow()
    {
        // Arrange
        using var meter = new IndexThinkingMeter();
        var metrics = new TurnMetrics
        {
            ThinkingTokens = 200,
            ContinuationCount = 2,
            Duration = TimeSpan.FromSeconds(5)
        };

        // Act
        var action = () => meter.RecordTurn(
            metrics,
            wasTruncated: true,
            complexity: TaskComplexity.Complex,
            sessionId: "test-session");

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void RecordTurn_WithNullMetrics_ShouldThrow()
    {
        // Arrange
        using var meter = new IndexThinkingMeter();

        // Act
        var action = () => meter.RecordTurn((TurnMetrics)null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("metrics");
    }

    [Fact]
    public void RecordTurn_WithZeroThinkingTokens_ShouldNotThrow()
    {
        // Arrange
        using var meter = new IndexThinkingMeter();
        var turnResult = CreateTurnResult(thinkingTokens: 0);

        // Act
        var action = () => meter.RecordTurn(turnResult);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void RecordTurn_WithTruncatedResult_ShouldNotThrow()
    {
        // Arrange
        using var meter = new IndexThinkingMeter();
        var turnResult = TurnResult.Truncated(
            CreateChatResponse(),
            new TurnMetrics
            {
                ThinkingTokens = 50,
                ContinuationCount = 3,
                Duration = TimeSpan.FromSeconds(10)
            });

        // Act
        var action = () => meter.RecordTurn(turnResult);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        // Arrange
        var meter = new IndexThinkingMeter();

        // Act
        var action = () => meter.Dispose();

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void Dispose_WhenCalledMultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var meter = new IndexThinkingMeter();
        meter.Dispose();

        // Act
        var action = () => meter.Dispose();

        // Assert
        action.Should().NotThrow();
    }

    private static TurnResult CreateTurnResult(
        int thinkingTokens = 100,
        int continuationCount = 0,
        TaskComplexity? complexity = TaskComplexity.Moderate)
    {
        return TurnResult.Success(
            CreateChatResponse(),
            new TurnMetrics
            {
                ThinkingTokens = thinkingTokens,
                ContinuationCount = continuationCount,
                Duration = TimeSpan.FromSeconds(1),
                DetectedComplexity = complexity
            });
    }

    private static ChatResponse CreateChatResponse()
    {
        return new ChatResponse([new ChatMessage(ChatRole.Assistant, "Test response")]);
    }
}
