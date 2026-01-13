using FluentAssertions;
using IndexThinking.Core;
using Xunit;

namespace IndexThinking.Tests.Core;

public class BudgetConfigTests
{
    [Fact]
    public void BudgetConfig_ShouldHaveReasonableDefaults()
    {
        // Arrange & Act
        var config = new BudgetConfig();

        // Assert
        config.ThinkingBudget.Should().Be(4096);
        config.AnswerBudget.Should().Be(4096);
        config.MaxContinuations.Should().Be(5);
        config.MaxDuration.Should().Be(TimeSpan.FromMinutes(10));
        config.MinProgressTokens.Should().Be(100);
    }

    [Fact]
    public void BudgetConfig_ShouldAllowCustomConfiguration()
    {
        // Arrange & Act
        var config = new BudgetConfig
        {
            ThinkingBudget = 8192,
            AnswerBudget = 2048,
            MaxContinuations = 10,
            MaxDuration = TimeSpan.FromMinutes(30),
            MinProgressTokens = 50
        };

        // Assert
        config.ThinkingBudget.Should().Be(8192);
        config.AnswerBudget.Should().Be(2048);
        config.MaxContinuations.Should().Be(10);
        config.MaxDuration.Should().Be(TimeSpan.FromMinutes(30));
        config.MinProgressTokens.Should().Be(50);
    }

    [Fact]
    public void BudgetConfig_WithMethod_ShouldPreserveOtherValues()
    {
        // Arrange
        var original = new BudgetConfig
        {
            ThinkingBudget = 8192,
            MaxContinuations = 3
        };

        // Act
        var modified = original with { AnswerBudget = 1024 };

        // Assert
        modified.ThinkingBudget.Should().Be(8192);
        modified.AnswerBudget.Should().Be(1024);
        modified.MaxContinuations.Should().Be(3);
    }

    [Fact]
    public void BudgetConfig_ForSimpleTask_ShouldBeLowBudget()
    {
        // Arrange - typical config for simple factual queries
        var config = new BudgetConfig
        {
            ThinkingBudget = 1024,
            AnswerBudget = 512,
            MaxContinuations = 1
        };

        // Assert
        config.ThinkingBudget.Should().BeLessThan(2048);
        config.MaxContinuations.Should().Be(1);
    }

    [Fact]
    public void BudgetConfig_ForResearchTask_ShouldBeHighBudget()
    {
        // Arrange - typical config for complex research tasks
        var config = new BudgetConfig
        {
            ThinkingBudget = 16384,
            AnswerBudget = 8192,
            MaxContinuations = 10,
            MaxDuration = TimeSpan.FromHours(1)
        };

        // Assert
        config.ThinkingBudget.Should().BeGreaterThan(8000);
        config.MaxContinuations.Should().BeGreaterThan(5);
        config.MaxDuration.Should().BeGreaterThan(TimeSpan.FromMinutes(30));
    }
}
