using FluentAssertions;
using IndexThinking.Client;
using IndexThinking.SimulationTests.Fixtures;
using Microsoft.Extensions.AI;
using Xunit;
using Xunit.Abstractions;

namespace IndexThinking.SimulationTests;

/// <summary>
/// Tests for IndexThinking pipeline features including
/// thinking content extraction, metrics tracking, and turn management.
/// </summary>
[Trait("Category", "Simulation")]
[Collection("Simulation")]
public class ThinkingPipelineTests : IClassFixture<SimulationTestFixture>
{
    private readonly SimulationTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public ThinkingPipelineTests(SimulationTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [GpuStackFact]
    public async Task GpuStack_ComplexReasoning_CapturesMetrics()
    {
        // Arrange
        using var client = _fixture.CreateGpuStackClient();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, """
                Solve this step by step:
                If a train travels 120 kilometers in 2 hours,
                and then 180 kilometers in 3 hours,
                what is the average speed for the entire journey?
                """)
        };

        // Act
        _output.WriteLine("Sending complex reasoning request...");
        var response = await client.GetResponseAsync(messages);

        // Assert
        response.Should().NotBeNull();
        response.Text.Should().NotBeNullOrWhiteSpace();
        _output.WriteLine($"Response: {response.Text}");

        // Verify turn metrics
        var metrics = response.GetTurnMetrics();
        metrics.Should().NotBeNull();
        metrics!.InputTokens.Should().BeGreaterThan(0);
        metrics.OutputTokens.Should().BeGreaterThan(0);

        _output.WriteLine($"Metrics: input={metrics.InputTokens}, thinking={metrics.ThinkingTokens}, output={metrics.OutputTokens}");
        _output.WriteLine($"Duration: {metrics.Duration.TotalMilliseconds:F0}ms");

        // Verify turn result
        var turnResult = response.GetTurnResult();
        turnResult.Should().NotBeNull();
        _output.WriteLine($"Truncated: {turnResult!.WasTruncated}");
    }

    [OpenAIFact]
    public async Task OpenAI_ComplexReasoning_CapturesMetrics()
    {
        // Arrange
        using var client = _fixture.CreateOpenAIClient();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, """
                Solve this step by step:
                If a train travels 120 kilometers in 2 hours,
                and then 180 kilometers in 3 hours,
                what is the average speed for the entire journey?
                """)
        };

        // Act
        _output.WriteLine("Sending complex reasoning request...");
        var response = await client.GetResponseAsync(messages);

        // Assert
        response.Should().NotBeNull();
        response.Text.Should().NotBeNullOrWhiteSpace();
        _output.WriteLine($"Response: {response.Text}");

        // Verify turn metrics
        var metrics = response.GetTurnMetrics();
        metrics.Should().NotBeNull();
        metrics!.InputTokens.Should().BeGreaterThan(0);
        metrics.OutputTokens.Should().BeGreaterThan(0);

        _output.WriteLine($"Metrics: input={metrics.InputTokens}, thinking={metrics.ThinkingTokens}, output={metrics.OutputTokens}");
        _output.WriteLine($"Duration: {metrics.Duration.TotalMilliseconds:F0}ms");
    }

    [GpuStackFact]
    public async Task GpuStack_WithSessionId_TracksSession()
    {
        // Arrange
        using var client = _fixture.CreateGpuStackClient();
        var sessionId = $"test-session-{Guid.NewGuid():N}"[..16];
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Remember that my favorite color is blue.")
        };

        var options = new ChatOptions();
        options.AdditionalProperties ??= [];
        options.AdditionalProperties["SessionId"] = sessionId;

        // Act
        _output.WriteLine($"Session ID: {sessionId}");
        var response = await client.GetResponseAsync(messages, options);

        // Assert
        response.Should().NotBeNull();
        _output.WriteLine($"Response: {response.Text}");

        var turnResult = response.GetTurnResult();
        turnResult.Should().NotBeNull();
    }

    [OpenAIFact]
    public async Task OpenAI_WithSessionId_TracksSession()
    {
        // Arrange
        using var client = _fixture.CreateOpenAIClient();
        var sessionId = $"test-session-{Guid.NewGuid():N}"[..16];
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Remember that my favorite color is blue.")
        };

        var options = new ChatOptions();
        options.AdditionalProperties ??= [];
        options.AdditionalProperties["SessionId"] = sessionId;

        // Act
        _output.WriteLine($"Session ID: {sessionId}");
        var response = await client.GetResponseAsync(messages, options);

        // Assert
        response.Should().NotBeNull();
        _output.WriteLine($"Response: {response.Text}");

        var turnResult = response.GetTurnResult();
        turnResult.Should().NotBeNull();
    }

    [GpuStackFact]
    public async Task GpuStack_CompareWithRawClient_ShowsPipelineOverhead()
    {
        // Arrange
        using var indexThinkingClient = _fixture.CreateGpuStackClient();
        using var rawClient = SimulationTestFixture.CreateRawGpuStackClient();
        var prompt = "What is 2 + 2?";

        // Act - Raw client
        var rawStart = DateTime.UtcNow;
        var rawResponse = await rawClient.GetResponseAsync([new ChatMessage(ChatRole.User, prompt)]);
        var rawDuration = DateTime.UtcNow - rawStart;

        _output.WriteLine($"[Raw] Response: {rawResponse.Text}");
        _output.WriteLine($"[Raw] Duration: {rawDuration.TotalMilliseconds:F0}ms");

        // Act - IndexThinking client
        var itStart = DateTime.UtcNow;
        var itResponse = await indexThinkingClient.GetResponseAsync([new ChatMessage(ChatRole.User, prompt)]);
        var itDuration = DateTime.UtcNow - itStart;

        _output.WriteLine($"[IndexThinking] Response: {itResponse.Text}");
        _output.WriteLine($"[IndexThinking] Duration: {itDuration.TotalMilliseconds:F0}ms");

        // Verify IndexThinking enriches the response
        var metrics = itResponse.GetTurnMetrics();
        metrics.Should().NotBeNull();
        _output.WriteLine($"[IndexThinking] Metrics captured: input={metrics?.InputTokens}, output={metrics?.OutputTokens}");

        // Assert both return valid responses
        rawResponse.Text.Should().Contain("4");
        itResponse.Text.Should().Contain("4");
    }

    [OpenAIFact]
    public async Task OpenAI_CompareWithRawClient_ShowsPipelineOverhead()
    {
        // Arrange
        using var indexThinkingClient = _fixture.CreateOpenAIClient();
        using var rawClient = SimulationTestFixture.CreateRawOpenAIClient();
        var prompt = "What is 2 + 2?";

        // Act - Raw client
        var rawStart = DateTime.UtcNow;
        var rawResponse = await rawClient.GetResponseAsync([new ChatMessage(ChatRole.User, prompt)]);
        var rawDuration = DateTime.UtcNow - rawStart;

        _output.WriteLine($"[Raw] Response: {rawResponse.Text}");
        _output.WriteLine($"[Raw] Duration: {rawDuration.TotalMilliseconds:F0}ms");

        // Act - IndexThinking client
        var itStart = DateTime.UtcNow;
        var itResponse = await indexThinkingClient.GetResponseAsync([new ChatMessage(ChatRole.User, prompt)]);
        var itDuration = DateTime.UtcNow - itStart;

        _output.WriteLine($"[IndexThinking] Response: {itResponse.Text}");
        _output.WriteLine($"[IndexThinking] Duration: {itDuration.TotalMilliseconds:F0}ms");

        // Verify IndexThinking enriches the response
        var metrics = itResponse.GetTurnMetrics();
        metrics.Should().NotBeNull();
        _output.WriteLine($"[IndexThinking] Metrics captured: input={metrics?.InputTokens}, output={metrics?.OutputTokens}");

        // Assert both return valid responses
        rawResponse.Text.Should().Contain("4");
        itResponse.Text.Should().Contain("4");
    }

    [AnthropicFact]
    public async Task Anthropic_ComplexReasoning_CapturesMetrics()
    {
        // Arrange
        using var client = _fixture.CreateAnthropicClient();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, """
                Solve this step by step:
                If a train travels 120 kilometers in 2 hours,
                and then 180 kilometers in 3 hours,
                what is the average speed for the entire journey?
                """)
        };

        // Act
        _output.WriteLine("Sending complex reasoning request to Anthropic...");
        var response = await client.GetResponseAsync(messages);

        // Assert
        response.Should().NotBeNull();
        response.Text.Should().NotBeNullOrWhiteSpace();
        _output.WriteLine($"Response: {response.Text}");

        // Verify turn metrics
        var metrics = response.GetTurnMetrics();
        metrics.Should().NotBeNull();
        metrics!.InputTokens.Should().BeGreaterThan(0);
        metrics.OutputTokens.Should().BeGreaterThan(0);

        _output.WriteLine($"Metrics: input={metrics.InputTokens}, thinking={metrics.ThinkingTokens}, output={metrics.OutputTokens}");
        _output.WriteLine($"Duration: {metrics.Duration.TotalMilliseconds:F0}ms");
    }

    [AnthropicFact]
    public async Task Anthropic_CompareWithRawClient_ShowsPipelineOverhead()
    {
        // Arrange
        using var indexThinkingClient = _fixture.CreateAnthropicClient();
        using var rawClient = SimulationTestFixture.CreateRawAnthropicClient();
        var prompt = "What is 2 + 2?";

        // Act - Raw client
        var rawStart = DateTime.UtcNow;
        var rawResponse = await rawClient.GetResponseAsync([new ChatMessage(ChatRole.User, prompt)]);
        var rawDuration = DateTime.UtcNow - rawStart;

        _output.WriteLine($"[Raw] Response: {rawResponse.Text}");
        _output.WriteLine($"[Raw] Duration: {rawDuration.TotalMilliseconds:F0}ms");

        // Act - IndexThinking client
        var itStart = DateTime.UtcNow;
        var itResponse = await indexThinkingClient.GetResponseAsync([new ChatMessage(ChatRole.User, prompt)]);
        var itDuration = DateTime.UtcNow - itStart;

        _output.WriteLine($"[IndexThinking] Response: {itResponse.Text}");
        _output.WriteLine($"[IndexThinking] Duration: {itDuration.TotalMilliseconds:F0}ms");

        // Verify IndexThinking enriches the response
        var metrics = itResponse.GetTurnMetrics();
        metrics.Should().NotBeNull();
        _output.WriteLine($"[IndexThinking] Metrics captured: input={metrics?.InputTokens}, output={metrics?.OutputTokens}");

        // Assert both return valid responses
        rawResponse.Text.Should().Contain("4");
        itResponse.Text.Should().Contain("4");
    }

    [GoogleFact]
    public async Task Google_ComplexReasoning_CapturesMetrics()
    {
        // Arrange
        using var client = _fixture.CreateGoogleClient();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, """
                Solve this step by step:
                If a train travels 120 kilometers in 2 hours,
                and then 180 kilometers in 3 hours,
                what is the average speed for the entire journey?
                """)
        };

        // Act
        _output.WriteLine("Sending complex reasoning request to Google Gemini...");
        var response = await client.GetResponseAsync(messages);

        // Assert
        response.Should().NotBeNull();
        response.Text.Should().NotBeNullOrWhiteSpace();
        _output.WriteLine($"Response: {response.Text}");

        // Verify turn metrics
        var metrics = response.GetTurnMetrics();
        metrics.Should().NotBeNull();
        metrics!.InputTokens.Should().BeGreaterThan(0);
        metrics.OutputTokens.Should().BeGreaterThan(0);

        _output.WriteLine($"Metrics: input={metrics.InputTokens}, thinking={metrics.ThinkingTokens}, output={metrics.OutputTokens}");
        _output.WriteLine($"Duration: {metrics.Duration.TotalMilliseconds:F0}ms");
    }

    [GoogleFact]
    public async Task Google_CompareWithRawClient_ShowsPipelineOverhead()
    {
        // Arrange
        using var indexThinkingClient = _fixture.CreateGoogleClient();
        using var rawClient = SimulationTestFixture.CreateRawGoogleClient();
        var prompt = "What is 2 + 2?";

        // Act - Raw client
        var rawStart = DateTime.UtcNow;
        var rawResponse = await rawClient.GetResponseAsync([new ChatMessage(ChatRole.User, prompt)]);
        var rawDuration = DateTime.UtcNow - rawStart;

        _output.WriteLine($"[Raw] Response: {rawResponse.Text}");
        _output.WriteLine($"[Raw] Duration: {rawDuration.TotalMilliseconds:F0}ms");

        // Act - IndexThinking client
        var itStart = DateTime.UtcNow;
        var itResponse = await indexThinkingClient.GetResponseAsync([new ChatMessage(ChatRole.User, prompt)]);
        var itDuration = DateTime.UtcNow - itStart;

        _output.WriteLine($"[IndexThinking] Response: {itResponse.Text}");
        _output.WriteLine($"[IndexThinking] Duration: {itDuration.TotalMilliseconds:F0}ms");

        // Verify IndexThinking enriches the response
        var metrics = itResponse.GetTurnMetrics();
        metrics.Should().NotBeNull();
        _output.WriteLine($"[IndexThinking] Metrics captured: input={metrics?.InputTokens}, output={metrics?.OutputTokens}");

        // Assert both return valid responses
        rawResponse.Text.Should().Contain("4");
        itResponse.Text.Should().Contain("4");
    }
}
