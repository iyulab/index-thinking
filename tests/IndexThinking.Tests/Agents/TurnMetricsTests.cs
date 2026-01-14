using IndexThinking.Agents;
using IndexThinking.Core;
using Xunit;

namespace IndexThinking.Tests.Agents;

public class TurnMetricsTests
{
    [Fact]
    public void TotalTokens_CalculatesCorrectly()
    {
        // Arrange
        var metrics = new TurnMetrics
        {
            InputTokens = 100,
            ThinkingTokens = 500,
            OutputTokens = 200
        };

        // Act & Assert
        Assert.Equal(800, metrics.TotalTokens);
    }

    [Fact]
    public void RequiredContinuation_WhenContinuationCountZero_ReturnsFalse()
    {
        // Arrange
        var metrics = new TurnMetrics { ContinuationCount = 0 };

        // Act & Assert
        Assert.False(metrics.RequiredContinuation);
    }

    [Fact]
    public void RequiredContinuation_WhenContinuationCountPositive_ReturnsTrue()
    {
        // Arrange
        var metrics = new TurnMetrics { ContinuationCount = 2 };

        // Act & Assert
        Assert.True(metrics.RequiredContinuation);
    }

    [Fact]
    public void AverageTokensPerContinuation_WithNoContinuations_ReturnsOutputTokens()
    {
        // Arrange
        var metrics = new TurnMetrics
        {
            OutputTokens = 500,
            ContinuationCount = 0
        };

        // Act & Assert
        Assert.Equal(500, metrics.AverageTokensPerContinuation);
    }

    [Fact]
    public void AverageTokensPerContinuation_WithContinuations_CalculatesCorrectly()
    {
        // Arrange
        var metrics = new TurnMetrics
        {
            OutputTokens = 900,
            ContinuationCount = 2 // 3 total responses (initial + 2 continuations)
        };

        // Act & Assert
        Assert.Equal(300, metrics.AverageTokensPerContinuation);
    }

    [Fact]
    public void Empty_ReturnsZeroValues()
    {
        // Act
        var metrics = TurnMetrics.Empty;

        // Assert
        Assert.Equal(0, metrics.InputTokens);
        Assert.Equal(0, metrics.ThinkingTokens);
        Assert.Equal(0, metrics.OutputTokens);
        Assert.Equal(0, metrics.TotalTokens);
        Assert.Equal(0, metrics.ContinuationCount);
        Assert.Equal(TimeSpan.Zero, metrics.Duration);
        Assert.Null(metrics.DetectedComplexity);
    }

    [Fact]
    public void Builder_AccumulatesCorrectly()
    {
        // Arrange
        var builder = TurnMetrics.CreateBuilder();

        // Act
        builder
            .WithInputTokens(100)
            .AddThinkingTokens(200)
            .AddThinkingTokens(300)
            .AddOutputTokens(150)
            .AddOutputTokens(50)
            .IncrementContinuation()
            .IncrementContinuation()
            .WithComplexity(TaskComplexity.Complex)
            .WithDuration(TimeSpan.FromSeconds(5));

        var metrics = builder.Build();

        // Assert
        Assert.Equal(100, metrics.InputTokens);
        Assert.Equal(500, metrics.ThinkingTokens);
        Assert.Equal(200, metrics.OutputTokens);
        Assert.Equal(2, metrics.ContinuationCount);
        Assert.Equal(TaskComplexity.Complex, metrics.DetectedComplexity);
        Assert.Equal(TimeSpan.FromSeconds(5), metrics.Duration);
    }

    [Fact]
    public void Builder_WithoutExplicitDuration_CalculatesDuration()
    {
        // Arrange
        var builder = TurnMetrics.CreateBuilder();

        // Act - small delay to ensure measurable duration
        Thread.Sleep(10);
        var metrics = builder.Build();

        // Assert
        Assert.True(metrics.Duration >= TimeSpan.FromMilliseconds(10));
    }

    [Fact]
    public void Equality_WorksCorrectly()
    {
        // Arrange
        var metrics1 = new TurnMetrics
        {
            InputTokens = 100,
            ThinkingTokens = 200,
            OutputTokens = 300,
            ContinuationCount = 1,
            Duration = TimeSpan.FromSeconds(5),
            DetectedComplexity = TaskComplexity.Moderate
        };

        var metrics2 = new TurnMetrics
        {
            InputTokens = 100,
            ThinkingTokens = 200,
            OutputTokens = 300,
            ContinuationCount = 1,
            Duration = TimeSpan.FromSeconds(5),
            DetectedComplexity = TaskComplexity.Moderate
        };

        // Act & Assert
        Assert.Equal(metrics1, metrics2);
        Assert.Equal(metrics1.GetHashCode(), metrics2.GetHashCode());
    }
}
