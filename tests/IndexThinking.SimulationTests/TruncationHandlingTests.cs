using FluentAssertions;
using IndexThinking.Client;
using IndexThinking.SimulationTests.Fixtures;
using Microsoft.Extensions.AI;
using Xunit;
using Xunit.Abstractions;

namespace IndexThinking.SimulationTests;

/// <summary>
/// Tests for truncation detection and continuation handling across different providers.
/// Each provider may return different finish reasons for max_tokens truncation.
/// </summary>
[Trait("Category", "Simulation")]
[Collection("Simulation")]
public class TruncationHandlingTests : IClassFixture<SimulationTestFixture>
{
    private readonly SimulationTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    // Prompt designed to generate long responses that exceed small token limits
    private const string LongResponsePrompt = """
        Write a detailed explanation of photosynthesis including:
        1. The light-dependent reactions
        2. The Calvin cycle
        3. The role of chlorophyll
        4. ATP and NADPH production
        5. Carbon fixation process
        Please be very thorough and include all biochemical details.
        """;

    public TruncationHandlingTests(SimulationTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [OpenAIFact]
    public async Task OpenAI_WithLowMaxTokens_DetectsTruncation()
    {
        // Arrange
        using var client = _fixture.CreateOpenAIClient();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, LongResponsePrompt)
        };

        var options = new ChatOptions
        {
            MaxOutputTokens = 50 // Very low limit to force truncation
        };

        // Act
        _output.WriteLine("Sending request with max_tokens=50...");
        var response = await client.GetResponseAsync(messages, options);

        // Assert
        response.Should().NotBeNull();
        _output.WriteLine($"Response: {response.Text}");
        _output.WriteLine($"Response length: {response.Text?.Length ?? 0} chars");

        var turnResult = response.GetTurnResult();
        turnResult.Should().NotBeNull();

        _output.WriteLine($"FinishReason: {response.FinishReason}");
        _output.WriteLine($"WasTruncated: {turnResult!.WasTruncated}");

        // OpenAI returns finish_reason: "length" when max_tokens is reached
        // This should map to ChatFinishReason.Length
        turnResult.WasTruncated.Should().BeTrue("OpenAI should detect truncation when max_tokens exceeded");
    }

    [AnthropicFact]
    public async Task Anthropic_WithLowMaxTokens_DetectsTruncation()
    {
        // Arrange
        using var client = _fixture.CreateAnthropicClient();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, LongResponsePrompt)
        };

        var options = new ChatOptions
        {
            MaxOutputTokens = 50 // Very low limit to force truncation
        };

        // Act
        _output.WriteLine("Sending request with max_tokens=50...");
        var response = await client.GetResponseAsync(messages, options);

        // Assert
        response.Should().NotBeNull();
        _output.WriteLine($"Response: {response.Text}");
        _output.WriteLine($"Response length: {response.Text?.Length ?? 0} chars");

        var turnResult = response.GetTurnResult();
        turnResult.Should().NotBeNull();

        _output.WriteLine($"FinishReason: {response.FinishReason}");
        _output.WriteLine($"WasTruncated: {turnResult!.WasTruncated}");

        // Anthropic returns stop_reason: "max_tokens" when limit is reached
        turnResult.WasTruncated.Should().BeTrue("Anthropic should detect truncation when max_tokens exceeded");
    }

    [GoogleFact]
    public async Task Google_WithLowMaxTokens_DetectsTruncation()
    {
        // Arrange
        using var client = _fixture.CreateGoogleClient();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, LongResponsePrompt)
        };

        var options = new ChatOptions
        {
            MaxOutputTokens = 50 // Very low limit to force truncation
        };

        // Act
        _output.WriteLine("Sending request with max_tokens=50...");
        var response = await client.GetResponseAsync(messages, options);

        // Assert
        response.Should().NotBeNull();
        _output.WriteLine($"Response: {response.Text}");
        _output.WriteLine($"Response length: {response.Text?.Length ?? 0} chars");

        var turnResult = response.GetTurnResult();
        turnResult.Should().NotBeNull();

        _output.WriteLine($"FinishReason: {response.FinishReason}");
        _output.WriteLine($"WasTruncated: {turnResult!.WasTruncated}");

        // Google Gemini returns finishReason: "MAX_TOKENS" when limit is reached
        turnResult.WasTruncated.Should().BeTrue("Google should detect truncation when max_tokens exceeded");
    }

    [GpuStackFact]
    public async Task GpuStack_WithLowMaxTokens_DetectsTruncation()
    {
        // Arrange
        using var client = _fixture.CreateGpuStackClient();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, LongResponsePrompt)
        };

        var options = new ChatOptions
        {
            MaxOutputTokens = 50 // Very low limit to force truncation
        };

        // Act
        _output.WriteLine("Sending request with max_tokens=50...");
        var response = await client.GetResponseAsync(messages, options);

        // Assert
        response.Should().NotBeNull();
        _output.WriteLine($"Response: {response.Text}");
        _output.WriteLine($"Response length: {response.Text?.Length ?? 0} chars");

        var turnResult = response.GetTurnResult();
        turnResult.Should().NotBeNull();

        _output.WriteLine($"FinishReason: {response.FinishReason}");
        _output.WriteLine($"WasTruncated: {turnResult!.WasTruncated}");

        // GPUStack (OpenAI-compatible) returns finish_reason: "length"
        turnResult.WasTruncated.Should().BeTrue("GPUStack should detect truncation when max_tokens exceeded");
    }

    [OpenAIFact]
    public async Task OpenAI_TruncatedResponse_ReportsCorrectReason()
    {
        // Arrange
        using var client = _fixture.CreateOpenAIClient();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Count from 1 to 1000, one number per line.")
        };

        var options = new ChatOptions
        {
            MaxOutputTokens = 30
        };

        // Act
        var response = await client.GetResponseAsync(messages, options);

        // Assert
        var turnResult = response.GetTurnResult();
        _output.WriteLine($"Response: {response.Text}");
        _output.WriteLine($"FinishReason: {response.FinishReason}");
        _output.WriteLine($"FinishReason.Value: {response.FinishReason?.Value}");
        _output.WriteLine($"WasTruncated: {turnResult?.WasTruncated}");

        response.FinishReason.Should().Be(ChatFinishReason.Length);
    }

    [AnthropicFact]
    public async Task Anthropic_TruncatedResponse_ReportsCorrectReason()
    {
        // Arrange
        using var client = _fixture.CreateAnthropicClient();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Count from 1 to 1000, one number per line.")
        };

        var options = new ChatOptions
        {
            MaxOutputTokens = 30
        };

        // Act
        var response = await client.GetResponseAsync(messages, options);

        // Assert
        var turnResult = response.GetTurnResult();
        _output.WriteLine($"Response: {response.Text}");
        _output.WriteLine($"FinishReason: {response.FinishReason}");
        _output.WriteLine($"FinishReason.Value: {response.FinishReason?.Value}");
        _output.WriteLine($"WasTruncated: {turnResult?.WasTruncated}");

        // Anthropic may return "max_tokens" as the stop_reason
        var isLengthOrMaxTokens = response.FinishReason == ChatFinishReason.Length ||
                                   response.FinishReason?.Value == "max_tokens";
        isLengthOrMaxTokens.Should().BeTrue("Anthropic should return length or max_tokens finish reason");
    }

    [GoogleFact]
    public async Task Google_TruncatedResponse_ReportsCorrectReason()
    {
        // Arrange
        using var client = _fixture.CreateGoogleClient();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Count from 1 to 1000, one number per line.")
        };

        var options = new ChatOptions
        {
            MaxOutputTokens = 30
        };

        // Act
        var response = await client.GetResponseAsync(messages, options);

        // Assert
        var turnResult = response.GetTurnResult();
        _output.WriteLine($"Response: {response.Text}");
        _output.WriteLine($"FinishReason: {response.FinishReason}");
        _output.WriteLine($"FinishReason.Value: {response.FinishReason?.Value}");
        _output.WriteLine($"WasTruncated: {turnResult?.WasTruncated}");

        // Google may return "MAX_TOKENS" or map to ChatFinishReason.Length
        var isLengthOrMaxTokens = response.FinishReason == ChatFinishReason.Length ||
                                   response.FinishReason?.Value?.Contains("MAX", StringComparison.OrdinalIgnoreCase) == true;
        isLengthOrMaxTokens.Should().BeTrue("Google should return length or MAX_TOKENS finish reason");
    }
}
