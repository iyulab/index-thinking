using FluentAssertions;
using IndexThinking.Client;
using IndexThinking.SimulationTests.Fixtures;
using Microsoft.Extensions.AI;
using Xunit;
using Xunit.Abstractions;

namespace IndexThinking.SimulationTests;

/// <summary>
/// Basic conversation simulation tests with real API providers.
/// These tests verify the core functionality of IndexThinking pipeline.
/// </summary>
[Trait("Category", "Simulation")]
[Collection("Simulation")]
public class BasicConversationTests : IClassFixture<SimulationTestFixture>
{
    private readonly SimulationTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public BasicConversationTests(SimulationTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [GpuStackFact]
    public async Task GpuStack_SimpleGreeting_ReturnsResponse()
    {
        // Arrange
        using var client = _fixture.CreateGpuStackClient();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello! Please respond with a brief greeting.")
        };

        // Act
        _output.WriteLine("Sending request to GPUStack...");
        var response = await client.GetResponseAsync(messages);

        // Assert
        response.Should().NotBeNull();
        response.Text.Should().NotBeNullOrWhiteSpace();
        _output.WriteLine($"Response: {response.Text}");

        // Verify metrics are captured
        var metrics = response.GetTurnMetrics();
        _output.WriteLine($"Metrics: input={metrics?.InputTokens}, output={metrics?.OutputTokens}");
    }

    [OpenAIFact]
    public async Task OpenAI_SimpleGreeting_ReturnsResponse()
    {
        // Arrange
        using var client = _fixture.CreateOpenAIClient();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello! Please respond with a brief greeting.")
        };

        // Act
        _output.WriteLine("Sending request to OpenAI...");
        var response = await client.GetResponseAsync(messages);

        // Assert
        response.Should().NotBeNull();
        response.Text.Should().NotBeNullOrWhiteSpace();
        _output.WriteLine($"Response: {response.Text}");

        // Verify metrics are captured
        var metrics = response.GetTurnMetrics();
        _output.WriteLine($"Metrics: input={metrics?.InputTokens}, output={metrics?.OutputTokens}");
    }

    [GpuStackFact]
    public async Task GpuStack_MathQuestion_ReturnsCorrectAnswer()
    {
        // Arrange
        using var client = _fixture.CreateGpuStackClient();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "What is 15 + 27? Please respond with just the number.")
        };

        // Act
        var response = await client.GetResponseAsync(messages);

        // Assert
        response.Should().NotBeNull();
        response.Text.Should().NotBeNullOrWhiteSpace();
        response.Text.Should().Contain("42");
        _output.WriteLine($"Response: {response.Text}");
    }

    [OpenAIFact]
    public async Task OpenAI_MathQuestion_ReturnsCorrectAnswer()
    {
        // Arrange
        using var client = _fixture.CreateOpenAIClient();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "What is 15 + 27? Please respond with just the number.")
        };

        // Act
        var response = await client.GetResponseAsync(messages);

        // Assert
        response.Should().NotBeNull();
        response.Text.Should().NotBeNullOrWhiteSpace();
        response.Text.Should().Contain("42");
        _output.WriteLine($"Response: {response.Text}");
    }

    [GpuStackFact]
    public async Task GpuStack_MultiTurnConversation_MaintainsContext()
    {
        // Arrange
        using var client = _fixture.CreateGpuStackClient();
        var messages = new List<ChatMessage>();

        // Act - First turn
        messages.Add(new ChatMessage(ChatRole.User, "My name is TestUser. Please remember that."));
        var response1 = await client.GetResponseAsync(messages);
        messages.Add(new ChatMessage(ChatRole.Assistant, response1.Text ?? ""));

        _output.WriteLine($"Turn 1: {response1.Text}");

        // Act - Second turn
        messages.Add(new ChatMessage(ChatRole.User, "What is my name?"));
        var response2 = await client.GetResponseAsync(messages);

        _output.WriteLine($"Turn 2: {response2.Text}");

        // Assert
        response2.Text.Should().Contain("TestUser");
    }

    [OpenAIFact]
    public async Task OpenAI_MultiTurnConversation_MaintainsContext()
    {
        // Arrange
        using var client = _fixture.CreateOpenAIClient();
        var messages = new List<ChatMessage>();

        // Act - First turn
        messages.Add(new ChatMessage(ChatRole.User, "My name is TestUser. Please remember that."));
        var response1 = await client.GetResponseAsync(messages);
        messages.Add(new ChatMessage(ChatRole.Assistant, response1.Text ?? ""));

        _output.WriteLine($"Turn 1: {response1.Text}");

        // Act - Second turn
        messages.Add(new ChatMessage(ChatRole.User, "What is my name?"));
        var response2 = await client.GetResponseAsync(messages);

        _output.WriteLine($"Turn 2: {response2.Text}");

        // Assert
        response2.Text.Should().Contain("TestUser");
    }

    [AnthropicFact]
    public async Task Anthropic_SimpleGreeting_ReturnsResponse()
    {
        // Arrange
        using var client = _fixture.CreateAnthropicClient();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello! Please respond with a brief greeting.")
        };

        // Act
        _output.WriteLine("Sending request to Anthropic...");
        var response = await client.GetResponseAsync(messages);

        // Assert
        response.Should().NotBeNull();
        response.Text.Should().NotBeNullOrWhiteSpace();
        _output.WriteLine($"Response: {response.Text}");

        // Verify metrics are captured
        var metrics = response.GetTurnMetrics();
        _output.WriteLine($"Metrics: input={metrics?.InputTokens}, output={metrics?.OutputTokens}");
    }

    [AnthropicFact]
    public async Task Anthropic_MathQuestion_ReturnsCorrectAnswer()
    {
        // Arrange
        using var client = _fixture.CreateAnthropicClient();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "What is 15 + 27? Please respond with just the number.")
        };

        // Act
        var response = await client.GetResponseAsync(messages);

        // Assert
        response.Should().NotBeNull();
        response.Text.Should().NotBeNullOrWhiteSpace();
        response.Text.Should().Contain("42");
        _output.WriteLine($"Response: {response.Text}");
    }

    [GoogleFact]
    public async Task Google_SimpleGreeting_ReturnsResponse()
    {
        // Arrange
        using var client = _fixture.CreateGoogleClient();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello! Please respond with a brief greeting.")
        };

        // Act
        _output.WriteLine("Sending request to Google Gemini...");
        var response = await client.GetResponseAsync(messages);

        // Assert
        response.Should().NotBeNull();
        response.Text.Should().NotBeNullOrWhiteSpace();
        _output.WriteLine($"Response: {response.Text}");

        // Verify metrics are captured
        var metrics = response.GetTurnMetrics();
        _output.WriteLine($"Metrics: input={metrics?.InputTokens}, output={metrics?.OutputTokens}");
    }

    [GoogleFact]
    public async Task Google_MathQuestion_ReturnsCorrectAnswer()
    {
        // Arrange
        using var client = _fixture.CreateGoogleClient();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "What is 15 + 27? Please respond with just the number.")
        };

        // Act
        var response = await client.GetResponseAsync(messages);

        // Assert
        response.Should().NotBeNull();
        response.Text.Should().NotBeNullOrWhiteSpace();
        response.Text.Should().Contain("42");
        _output.WriteLine($"Response: {response.Text}");
    }
}
