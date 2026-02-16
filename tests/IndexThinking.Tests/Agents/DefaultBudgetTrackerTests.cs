using IndexThinking.Abstractions;
using IndexThinking.Agents;
using IndexThinking.Core;
using Microsoft.Extensions.AI;
using NSubstitute;
using Xunit;

namespace IndexThinking.Tests.Agents;

public class DefaultBudgetTrackerTests
{
    private readonly ITokenCounter _tokenCounter;
    private readonly DefaultBudgetTracker _tracker;

    public DefaultBudgetTrackerTests()
    {
        _tokenCounter = Substitute.For<ITokenCounter>();
        _tokenCounter.Count(Arg.Any<string>()).Returns(100);
        _tracker = new DefaultBudgetTracker(_tokenCounter);
    }

    [Fact]
    public void RecordResponse_WithUsageMetadata_UsesActualCounts()
    {
        // Arrange
        var usage = new UsageDetails
        {
            InputTokenCount = 50,
            OutputTokenCount = 150
        };
        var response = CreateResponseWithUsage("Test response", usage);

        // Act
        _tracker.RecordResponse(response, null);
        var result = _tracker.GetUsage();

        // Assert
        Assert.Equal(150, result.OutputTokens);
        _tokenCounter.DidNotReceive().Count(Arg.Any<string>());
    }

    [Fact]
    public void RecordResponse_WithoutUsage_EstimatesTokens()
    {
        // Arrange
        _tokenCounter.Count("Estimated response").Returns(75);
        var response = CreateResponse("Estimated response");

        // Act
        _tracker.RecordResponse(response, null);
        var result = _tracker.GetUsage();

        // Assert
        Assert.Equal(75, result.OutputTokens);
        _tokenCounter.Received(1).Count("Estimated response");
    }

    [Fact]
    public void RecordResponse_WithThinkingContent_AddsThinkingTokens()
    {
        // Arrange
        _tokenCounter.Count("Response text").Returns(50);
        _tokenCounter.Count("My thinking process").Returns(200);
        var response = CreateResponse("Response text");
        var thinking = new ThinkingContent { Text = "My thinking process" };

        // Act
        _tracker.RecordResponse(response, thinking);
        var result = _tracker.GetUsage();

        // Assert
        Assert.Equal(200, result.ThinkingTokens);
        Assert.Equal(50, result.OutputTokens);
    }

    [Fact]
    public void GetUsage_AfterMultipleResponses_AccumulatesCorrectly()
    {
        // Arrange
        _tokenCounter.Count(Arg.Any<string>()).Returns(100);

        // Act
        _tracker.SetInputTokens(50);
        _tracker.RecordResponse(CreateResponse("First"), null);
        _tracker.RecordResponse(CreateResponse("Second"), null);
        _tracker.RecordResponse(CreateResponse("Third"), null);

        var result = _tracker.GetUsage();

        // Assert
        Assert.Equal(50, result.InputTokens);
        Assert.Equal(300, result.OutputTokens);
        Assert.Equal(350, result.TotalTokens);
    }

    [Fact]
    public void IsThinkingBudgetExceeded_UnderBudget_ReturnsFalse()
    {
        // Arrange
        _tokenCounter.Count(Arg.Any<string>()).Returns(100);
        var thinking = new ThinkingContent { Text = "Some thinking" };
        _tracker.RecordResponse(CreateResponse("Response"), thinking);
        var config = new BudgetConfig { ThinkingBudget = 200 };

        // Act
        var result = _tracker.IsThinkingBudgetExceeded(config);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsThinkingBudgetExceeded_OverBudget_ReturnsTrue()
    {
        // Arrange
        _tokenCounter.Count(Arg.Any<string>()).Returns(500);
        var thinking = new ThinkingContent { Text = "Long thinking process" };
        _tracker.RecordResponse(CreateResponse("Response"), thinking);
        var config = new BudgetConfig { ThinkingBudget = 100 };

        // Act
        var result = _tracker.IsThinkingBudgetExceeded(config);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsAnswerBudgetExceeded_UnderBudget_ReturnsFalse()
    {
        // Arrange
        _tokenCounter.Count(Arg.Any<string>()).Returns(100);
        _tracker.RecordResponse(CreateResponse("Short response"), null);
        var config = new BudgetConfig { AnswerBudget = 200 };

        // Act
        var result = _tracker.IsAnswerBudgetExceeded(config);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsAnswerBudgetExceeded_OverBudget_ReturnsTrue()
    {
        // Arrange
        _tokenCounter.Count(Arg.Any<string>()).Returns(500);
        _tracker.RecordResponse(CreateResponse("Very long response"), null);
        var config = new BudgetConfig { AnswerBudget = 100 };

        // Act
        var result = _tracker.IsAnswerBudgetExceeded(config);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Reset_ClearsAllCounters()
    {
        // Arrange
        _tokenCounter.Count(Arg.Any<string>()).Returns(100);
        _tracker.SetInputTokens(50);
        _tracker.RecordResponse(CreateResponse("Response"), new ThinkingContent { Text = "Thinking" });

        // Act
        _tracker.Reset();
        var result = _tracker.GetUsage();

        // Assert
        Assert.Equal(0, result.InputTokens);
        Assert.Equal(0, result.ThinkingTokens);
        Assert.Equal(0, result.OutputTokens);
        Assert.Equal(0, result.TotalTokens);
    }

    [Fact]
    public void Constructor_NullTokenCounter_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new DefaultBudgetTracker(null!));
    }

    [Fact]
    public void RecordResponse_NullResponse_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _tracker.RecordResponse(null!, null));
    }

    [Fact]
    public void BudgetUsage_Empty_ReturnsZeroValues()
    {
        // Act
        var empty = BudgetUsage.Empty;

        // Assert
        Assert.Equal(0, empty.InputTokens);
        Assert.Equal(0, empty.ThinkingTokens);
        Assert.Equal(0, empty.OutputTokens);
        Assert.Equal(0, empty.TotalTokens);
    }

    private static ChatResponse CreateResponse(string text)
    {
        var message = new ChatMessage(ChatRole.Assistant, text);
        return new ChatResponse([message]);
    }

    private static ChatResponse CreateResponseWithUsage(string text, UsageDetails usage)
    {
        var message = new ChatMessage(ChatRole.Assistant, text);
        return new ChatResponse([message]) { Usage = usage };
    }
}
