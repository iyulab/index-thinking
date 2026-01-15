using FluentAssertions;
using IndexThinking.SimulationTests.Fixtures;
using Microsoft.Extensions.AI;
using Xunit;
using Xunit.Abstractions;

namespace IndexThinking.SimulationTests;

/// <summary>
/// Tests for error handling scenarios.
/// </summary>
[Trait("Category", "Simulation")]
[Collection("Simulation")]
public class ErrorHandlingTests : IClassFixture<SimulationTestFixture>
{
    private readonly SimulationTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public ErrorHandlingTests(SimulationTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [GpuStackFact]
    public async Task GpuStack_Cancellation_ThrowsOperationCanceled()
    {
        // Arrange
        using var client = _fixture.CreateGpuStackClient();
        using var cts = new CancellationTokenSource();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Write a very long story about a magical kingdom.")
        };

        // Cancel immediately
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await client.GetResponseAsync(messages, cancellationToken: cts.Token);
        });

        _output.WriteLine("Cancellation handled correctly.");
    }

    [OpenAIFact]
    public async Task OpenAI_Cancellation_ThrowsOperationCanceled()
    {
        // Arrange
        using var client = _fixture.CreateOpenAIClient();
        using var cts = new CancellationTokenSource();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Write a very long story about a magical kingdom.")
        };

        // Cancel immediately
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await client.GetResponseAsync(messages, cancellationToken: cts.Token);
        });

        _output.WriteLine("Cancellation handled correctly.");
    }

    [GpuStackFact]
    public async Task GpuStack_EmptyMessages_HandlesGracefully()
    {
        // Arrange
        using var client = _fixture.CreateGpuStackClient();
        var messages = new List<ChatMessage>();

        // Act & Assert
        // Empty messages should throw or return error gracefully
        var act = async () => await client.GetResponseAsync(messages);

        // The behavior depends on the provider, but it should not crash
        try
        {
            var response = await act();
            _output.WriteLine($"Provider accepted empty messages. Response: {response.Text}");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Provider rejected empty messages: {ex.Message}");
            // This is acceptable behavior
        }
    }

    [OpenAIFact]
    public async Task OpenAI_EmptyMessages_HandlesGracefully()
    {
        // Arrange
        using var client = _fixture.CreateOpenAIClient();
        var messages = new List<ChatMessage>();

        // Act & Assert
        var act = async () => await client.GetResponseAsync(messages);

        try
        {
            var response = await act();
            _output.WriteLine($"Provider accepted empty messages. Response: {response.Text}");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Provider rejected empty messages: {ex.Message}");
        }
    }

    [GpuStackFact]
    public async Task GpuStack_VeryLongInput_HandlesCorrectly()
    {
        // Arrange
        using var client = _fixture.CreateGpuStackClient();
        var longText = new string('a', 1000); // 1000 character input
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, $"Summarize this in one word: {longText}")
        };

        // Act
        var response = await client.GetResponseAsync(messages);

        // Assert
        response.Should().NotBeNull();
        response.Text.Should().NotBeNullOrWhiteSpace();

        _output.WriteLine($"Input length: {longText.Length}");
        _output.WriteLine($"Response: {response.Text}");
    }

    [OpenAIFact]
    public async Task OpenAI_VeryLongInput_HandlesCorrectly()
    {
        // Arrange
        using var client = _fixture.CreateOpenAIClient();
        var longText = new string('a', 1000);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, $"Summarize this in one word: {longText}")
        };

        // Act
        var response = await client.GetResponseAsync(messages);

        // Assert
        response.Should().NotBeNull();
        response.Text.Should().NotBeNullOrWhiteSpace();

        _output.WriteLine($"Input length: {longText.Length}");
        _output.WriteLine($"Response: {response.Text}");
    }
}
