using IndexThinking.Agents;
using IndexThinking.Core;
using Microsoft.Extensions.AI;
using Xunit;

namespace IndexThinking.Tests.Agents;

public class TurnResultTests
{
    private static ChatResponse CreateTestResponse(string text = "Test response")
    {
        var message = new ChatMessage(ChatRole.Assistant, text);
        return new ChatResponse([message]);
    }

    [Fact]
    public void Success_CreatesNonTruncatedResult()
    {
        // Arrange
        var response = CreateTestResponse();
        var metrics = new TurnMetrics { OutputTokens = 100 };
        var thinking = new ThinkingContent { Text = "Thinking..." };

        // Act
        var result = TurnResult.Success(response, metrics, thinking);

        // Assert
        Assert.Same(response, result.Response);
        Assert.Same(metrics, result.Metrics);
        Assert.Same(thinking, result.ThinkingContent);
        Assert.False(result.WasTruncated);
        Assert.False(result.WasContinued);
    }

    [Fact]
    public void Truncated_CreatesTruncatedResult()
    {
        // Arrange
        var response = CreateTestResponse("Partial...");
        var metrics = new TurnMetrics { OutputTokens = 50, ContinuationCount = 5 };

        // Act
        var result = TurnResult.Truncated(response, metrics);

        // Assert
        Assert.Same(response, result.Response);
        Assert.True(result.WasTruncated);
        Assert.True(result.WasContinued);
    }

    [Fact]
    public void WasContinued_ReflectsContinuationCount()
    {
        // Arrange
        var response = CreateTestResponse();

        // Act
        var noContinuation = TurnResult.Success(response, new TurnMetrics { ContinuationCount = 0 });
        var withContinuation = TurnResult.Success(response, new TurnMetrics { ContinuationCount = 2 });

        // Assert
        Assert.False(noContinuation.WasContinued);
        Assert.True(withContinuation.WasContinued);
    }

    [Fact]
    public void ResponseText_ReturnsResponseText()
    {
        // Arrange
        var response = CreateTestResponse("Hello, world!");
        var result = TurnResult.Success(response, TurnMetrics.Empty);

        // Act & Assert
        Assert.Equal("Hello, world!", result.ResponseText);
    }

    [Fact]
    public void HasThinkingContent_WhenPresent_ReturnsTrue()
    {
        // Arrange
        var thinking = new ThinkingContent { Text = "Reasoning..." };
        var result = TurnResult.Success(CreateTestResponse(), TurnMetrics.Empty, thinking);

        // Act & Assert
        Assert.True(result.HasThinkingContent);
    }

    [Fact]
    public void HasThinkingContent_WhenAbsent_ReturnsFalse()
    {
        // Arrange
        var result = TurnResult.Success(CreateTestResponse(), TurnMetrics.Empty);

        // Act & Assert
        Assert.False(result.HasThinkingContent);
    }

    [Fact]
    public void HasReasoningState_WhenPresent_ReturnsTrue()
    {
        // Arrange
        var state = new ReasoningState
        {
            Provider = "openai",
            Data = [1, 2, 3]
        };
        var result = TurnResult.Success(CreateTestResponse(), TurnMetrics.Empty, reasoningState: state);

        // Act & Assert
        Assert.True(result.HasReasoningState);
    }

    [Fact]
    public void HasReasoningState_WhenAbsent_ReturnsFalse()
    {
        // Arrange
        var result = TurnResult.Success(CreateTestResponse(), TurnMetrics.Empty);

        // Act & Assert
        Assert.False(result.HasReasoningState);
    }

    [Fact]
    public void Context_CanBeAttached()
    {
        // Arrange
        var context = ThinkingContext.Create("session", [new(ChatRole.User, "Test")]);
        var response = CreateTestResponse();

        // Act
        var result = new TurnResult
        {
            Response = response,
            Metrics = TurnMetrics.Empty,
            Context = context
        };

        // Assert
        Assert.Same(context, result.Context);
    }

    [Fact]
    public void Equality_WorksCorrectly()
    {
        // Arrange
        var response = CreateTestResponse();
        var metrics = new TurnMetrics { OutputTokens = 100 };

        var result1 = new TurnResult
        {
            Response = response,
            Metrics = metrics,
            WasTruncated = false
        };

        var result2 = new TurnResult
        {
            Response = response,
            Metrics = metrics,
            WasTruncated = false
        };

        // Act & Assert
        Assert.Equal(result1, result2);
    }
}
