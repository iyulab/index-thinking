using IndexThinking.Abstractions;
using IndexThinking.Agents;
using IndexThinking.Core;
using Microsoft.Extensions.AI;
using NSubstitute;
using Xunit;

namespace IndexThinking.Tests.Agents;

public class HeuristicComplexityEstimatorTests
{
    private readonly ITokenCounter _tokenCounter;
    private readonly HeuristicComplexityEstimator _estimator;

    public HeuristicComplexityEstimatorTests()
    {
        _tokenCounter = Substitute.For<ITokenCounter>();
        _tokenCounter.Count(Arg.Any<string>()).Returns(100); // Default moderate length
        _estimator = new HeuristicComplexityEstimator(_tokenCounter);
    }

    [Fact]
    public void Estimate_ShortFactualQuestion_ReturnsSimple()
    {
        // Arrange
        _tokenCounter.Count(Arg.Any<string>()).Returns(30); // Short message
        var messages = CreateMessages("What is 2 + 2?");

        // Act
        var result = _estimator.Estimate(messages);

        // Assert
        Assert.Equal(TaskComplexity.Simple, result);
    }

    [Fact]
    public void Estimate_CodeWithDebugKeyword_ReturnsComplex()
    {
        // Arrange
        var messages = CreateMessages("Please debug this code:\n```python\ndef foo():\n    return bar\n```");

        // Act
        var result = _estimator.Estimate(messages);

        // Assert
        Assert.Equal(TaskComplexity.Complex, result);
    }

    [Fact]
    public void Estimate_ResearchQuestion_ReturnsResearch()
    {
        // Arrange
        var messages = CreateMessages("Please conduct comprehensive research on the impact of AI in healthcare and provide an in-depth analysis.");

        // Act
        var result = _estimator.Estimate(messages);

        // Assert
        Assert.Equal(TaskComplexity.Research, result);
    }

    [Fact]
    public void Estimate_ModerateLengthExplanation_ReturnsModerate()
    {
        // Arrange
        var messages = CreateMessages("Can you explain how dependency injection works in .NET?");

        // Act
        var result = _estimator.Estimate(messages);

        // Assert
        Assert.Equal(TaskComplexity.Moderate, result);
    }

    [Fact]
    public void Estimate_EmptyMessages_ReturnsSimple()
    {
        // Arrange
        var messages = new List<ChatMessage>();

        // Act
        var result = _estimator.Estimate(messages);

        // Assert
        Assert.Equal(TaskComplexity.Simple, result);
    }

    [Fact]
    public void Estimate_OnlyAssistantMessages_ReturnsSimple()
    {
        // Arrange
        var messages = new List<ChatMessage>
        {
            new(ChatRole.Assistant, "Hello, how can I help?")
        };

        // Act
        var result = _estimator.Estimate(messages);

        // Assert
        Assert.Equal(TaskComplexity.Simple, result);
    }

    [Fact]
    public void Estimate_LongMessage_IncreasesComplexity()
    {
        // Arrange
        _tokenCounter.Count(Arg.Any<string>()).Returns(600); // Long message
        var messages = CreateMessages("Summarize this document...");

        // Act
        var result = _estimator.Estimate(messages);

        // Assert
        // Moderate keyword (summarize:+1) + long message (+1) = score 2 = Complex
        Assert.Equal(TaskComplexity.Complex, result);
    }

    [Fact]
    public void Estimate_MultiTurnConversation_IncreasesComplexity()
    {
        // Arrange
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hi"),
            new(ChatRole.Assistant, "Hello!"),
            new(ChatRole.User, "Can you help?"),
            new(ChatRole.Assistant, "Sure!"),
            new(ChatRole.User, "Explain how React hooks work.")
        };

        // Act
        var result = _estimator.Estimate(messages);

        // Assert
        // 5 messages + moderate keyword = higher complexity
        Assert.True(result >= TaskComplexity.Moderate);
    }

    [Fact]
    public void GetRecommendedBudget_Simple_ReturnsSmallBudget()
    {
        // Act
        var budget = _estimator.GetRecommendedBudget(TaskComplexity.Simple);

        // Assert
        Assert.Equal(1024, budget.ThinkingBudget);
        Assert.Equal(2048, budget.AnswerBudget);
        Assert.Equal(2, budget.MaxContinuations);
    }

    [Fact]
    public void GetRecommendedBudget_Moderate_ReturnsModerateBudget()
    {
        // Act
        var budget = _estimator.GetRecommendedBudget(TaskComplexity.Moderate);

        // Assert
        Assert.Equal(4096, budget.ThinkingBudget);
        Assert.Equal(4096, budget.AnswerBudget);
    }

    [Fact]
    public void GetRecommendedBudget_Complex_ReturnsLargeBudget()
    {
        // Act
        var budget = _estimator.GetRecommendedBudget(TaskComplexity.Complex);

        // Assert
        Assert.Equal(8192, budget.ThinkingBudget);
        Assert.Equal(5, budget.MaxContinuations);
    }

    [Fact]
    public void GetRecommendedBudget_Research_ReturnsLargestBudget()
    {
        // Act
        var budget = _estimator.GetRecommendedBudget(TaskComplexity.Research);

        // Assert
        Assert.Equal(16384, budget.ThinkingBudget);
        Assert.Equal(8192, budget.AnswerBudget);
        Assert.Equal(7, budget.MaxContinuations);
    }

    [Fact]
    public void Estimate_ImplementKeyword_ReturnsComplex()
    {
        // Arrange
        var messages = CreateMessages("Implement a binary search algorithm in C#");

        // Act
        var result = _estimator.Estimate(messages);

        // Assert
        Assert.Equal(TaskComplexity.Complex, result);
    }

    [Fact]
    public void Constructor_NullTokenCounter_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new HeuristicComplexityEstimator(null!));
    }

    [Fact]
    public void Estimate_NullMessages_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _estimator.Estimate(null!));
    }

    private static List<ChatMessage> CreateMessages(string userMessage)
    {
        return [new ChatMessage(ChatRole.User, userMessage)];
    }
}
