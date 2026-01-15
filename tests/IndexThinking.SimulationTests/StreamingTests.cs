using System.Text;
using FluentAssertions;
using IndexThinking.Client;
using IndexThinking.SimulationTests.Fixtures;
using Microsoft.Extensions.AI;
using Xunit;
using Xunit.Abstractions;

namespace IndexThinking.SimulationTests;

/// <summary>
/// Tests for streaming response functionality.
/// </summary>
[Trait("Category", "Simulation")]
[Collection("Simulation")]
public class StreamingTests : IClassFixture<SimulationTestFixture>
{
    private readonly SimulationTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public StreamingTests(SimulationTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [GpuStackFact]
    public async Task GpuStack_StreamingResponse_ReceivesChunks()
    {
        // Arrange
        using var client = _fixture.CreateGpuStackClient();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Count from 1 to 5, one number per line.")
        };

        // Act
        var chunks = new List<string>();
        var fullText = new StringBuilder();

        _output.WriteLine("Streaming response...");

        await foreach (var update in client.GetStreamingResponseAsync(messages))
        {
            var text = update.Text ?? "";
            chunks.Add(text);
            fullText.Append(text);
            _output.WriteLine($"Chunk: '{text}'");
        }

        // Assert
        chunks.Should().NotBeEmpty();
        var result = fullText.ToString();
        result.Should().NotBeNullOrWhiteSpace();

        _output.WriteLine($"Total chunks: {chunks.Count}");
        _output.WriteLine($"Full response: {result}");
    }

    [OpenAIFact]
    public async Task OpenAI_StreamingResponse_ReceivesChunks()
    {
        // Arrange
        using var client = _fixture.CreateOpenAIClient();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Count from 1 to 5, one number per line.")
        };

        // Act
        var chunks = new List<string>();
        var fullText = new StringBuilder();

        _output.WriteLine("Streaming response...");

        await foreach (var update in client.GetStreamingResponseAsync(messages))
        {
            var text = update.Text ?? "";
            chunks.Add(text);
            fullText.Append(text);
            _output.WriteLine($"Chunk: '{text}'");
        }

        // Assert
        chunks.Should().NotBeEmpty();
        var result = fullText.ToString();
        result.Should().NotBeNullOrWhiteSpace();

        _output.WriteLine($"Total chunks: {chunks.Count}");
        _output.WriteLine($"Full response: {result}");
    }

    [GpuStackFact]
    public async Task GpuStack_StreamingLongResponse_HandlesCorrectly()
    {
        // Arrange
        using var client = _fixture.CreateGpuStackClient();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Write a short paragraph (2-3 sentences) about cats.")
        };

        // Act
        var chunks = new List<string>();
        var fullText = new StringBuilder();

        await foreach (var update in client.GetStreamingResponseAsync(messages))
        {
            var text = update.Text ?? "";
            chunks.Add(text);
            fullText.Append(text);
        }

        // Assert
        var result = fullText.ToString();
        result.Should().NotBeNullOrWhiteSpace();
        result.Length.Should().BeGreaterThan(50); // Should be a decent paragraph

        _output.WriteLine($"Total chunks: {chunks.Count}");
        _output.WriteLine($"Response length: {result.Length} chars");
        _output.WriteLine($"Response: {result}");
    }

    [OpenAIFact]
    public async Task OpenAI_StreamingLongResponse_HandlesCorrectly()
    {
        // Arrange
        using var client = _fixture.CreateOpenAIClient();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Write a short paragraph (2-3 sentences) about cats.")
        };

        // Act
        var chunks = new List<string>();
        var fullText = new StringBuilder();

        await foreach (var update in client.GetStreamingResponseAsync(messages))
        {
            var text = update.Text ?? "";
            chunks.Add(text);
            fullText.Append(text);
        }

        // Assert
        var result = fullText.ToString();
        result.Should().NotBeNullOrWhiteSpace();
        result.Length.Should().BeGreaterThan(50); // Should be a decent paragraph

        _output.WriteLine($"Total chunks: {chunks.Count}");
        _output.WriteLine($"Response length: {result.Length} chars");
        _output.WriteLine($"Response: {result}");
    }
}
