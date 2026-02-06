using FluentAssertions;
using IndexThinking.Abstractions;
using IndexThinking.Agents;
using IndexThinking.Client;
using IndexThinking.Context;
using IndexThinking.Extensions;
using IndexThinking.IntegrationTests.Fixtures;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IndexThinking.IntegrationTests;

/// <summary>
/// Integration tests for streaming with thinking orchestration (v0.16.0).
/// Verifies the Collect-and-Yield pattern: chunks are yielded to caller
/// while buffered internally for post-stream orchestration.
/// </summary>
public class StreamingOrchestrationTests
{
    [Fact]
    public async Task Streaming_SimpleRequest_YieldsChunksAndMetadata()
    {
        // Arrange
        var innerClient = new MockChatClient()
            .WithResponse("Hello how can I help you today");

        var services = new ServiceCollection()
            .AddIndexThinkingAgents()
            .AddIndexThinkingInMemoryStorage()
            .BuildServiceProvider();

        var client = new ChatClientBuilder(innerClient)
            .UseIndexThinking()
            .Build(services);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hi there")
        };

        // Act
        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in client.GetStreamingResponseAsync(messages))
        {
            updates.Add(update);
        }

        // Assert - should have content chunks plus a metadata chunk
        updates.Should().HaveCountGreaterThanOrEqualTo(2);

        // Content chunks should contain text
        var contentUpdates = updates.Where(u => u.Text is not null && u.Text.Trim().Length > 0).ToList();
        contentUpdates.Should().NotBeEmpty();

        // Last update should contain TurnResult metadata
        var lastUpdate = updates.Last();
        lastUpdate.AdditionalProperties.Should().NotBeNull();
        lastUpdate.AdditionalProperties!.Should().ContainKey(ThinkingChatClient.TurnResultKey);

        var turnResult = lastUpdate.AdditionalProperties[ThinkingChatClient.TurnResultKey] as TurnResult;
        turnResult.Should().NotBeNull();
        turnResult!.WasTruncated.Should().BeFalse();
    }

    [Fact]
    public async Task Streaming_WithMetricsEnabled_IncludesMetricsInFinalUpdate()
    {
        // Arrange
        var innerClient = new MockChatClient()
            .WithResponse("Response with metrics tracking");

        var services = new ServiceCollection()
            .AddIndexThinkingAgents()
            .AddIndexThinkingInMemoryStorage()
            .BuildServiceProvider();

        var client = new ChatClientBuilder(innerClient)
            .UseIndexThinking(options =>
            {
                options.IncludeMetricsInMetadata = true;
            })
            .Build(services);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Test message")
        };

        // Act
        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in client.GetStreamingResponseAsync(messages))
        {
            updates.Add(update);
        }

        // Assert
        var lastUpdate = updates.Last();
        lastUpdate.AdditionalProperties.Should().ContainKey(ThinkingChatClient.TurnMetricsKey);

        var metrics = lastUpdate.AdditionalProperties![ThinkingChatClient.TurnMetricsKey] as TurnMetrics;
        metrics.Should().NotBeNull();
    }

    [Fact]
    public async Task Streaming_WithSessionTracking_TracksConversation()
    {
        // Arrange
        var innerClient = new MockChatClient()
            .WithResponse("First streaming response")
            .WithResponse("Second streaming response");

        var services = new ServiceCollection()
            .AddIndexThinkingAgents()
            .AddIndexThinkingInMemoryStorage()
            .AddIndexThinkingContext()
            .BuildServiceProvider();

        var contextTracker = services.GetRequiredService<IContextTracker>();
        var contextInjector = services.GetRequiredService<IContextInjector>();
        var turnManager = services.GetRequiredService<IThinkingTurnManager>();

        var options = new ThinkingChatClientOptions
        {
            EnableContextTracking = true,
            EnableContextInjection = true
        };

        var client = new ThinkingChatClient(
            innerClient,
            turnManager,
            options,
            contextTracker,
            contextInjector);

        var sessionOptions = ThinkingChatClientExtensions.WithSession("streaming-session-1");

        // Act - First streaming request
        await foreach (var _ in client.GetStreamingResponseAsync(
            [new ChatMessage(ChatRole.User, "Hello via stream")],
            sessionOptions)) { }

        // Act - Second streaming request
        await foreach (var _ in client.GetStreamingResponseAsync(
            [new ChatMessage(ChatRole.User, "Second message")],
            sessionOptions)) { }

        // Assert - Context should track both turns
        var context = contextTracker.GetContext("streaming-session-1");
        context.RecentTurns.Should().HaveCount(2);
    }

    [Fact]
    public async Task Streaming_NullMessages_Throws()
    {
        // Arrange
        var innerClient = new MockChatClient();
        var services = new ServiceCollection()
            .AddIndexThinkingAgents()
            .AddIndexThinkingInMemoryStorage()
            .BuildServiceProvider();

        var client = new ChatClientBuilder(innerClient)
            .UseIndexThinking()
            .Build(services);

        // Act & Assert
        var action = async () =>
        {
            await foreach (var _ in client.GetStreamingResponseAsync(null!)) { }
        };

        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Streaming_ContentTextIsPreserved()
    {
        // Arrange
        var innerClient = new MockChatClient()
            .WithResponse("The quick brown fox jumps");

        var services = new ServiceCollection()
            .AddIndexThinkingAgents()
            .AddIndexThinkingInMemoryStorage()
            .BuildServiceProvider();

        var client = new ChatClientBuilder(innerClient)
            .UseIndexThinking()
            .Build(services);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Tell me something")
        };

        // Act
        var textParts = new List<string>();
        await foreach (var update in client.GetStreamingResponseAsync(messages))
        {
            if (update.Text is not null)
            {
                textParts.Add(update.Text);
            }
        }

        // Assert - All words should be present in streamed content
        var fullText = string.Join("", textParts);
        fullText.Should().Contain("quick");
        fullText.Should().Contain("brown");
        fullText.Should().Contain("fox");
    }

    [Fact]
    public async Task Streaming_WithReasoningEnabled_AppliesReasoningModification()
    {
        // Arrange
        var innerClient = new MockChatClient()
            .WithResponse("Reasoning response");

        var services = new ServiceCollection()
            .AddIndexThinkingAgents()
            .AddIndexThinkingInMemoryStorage()
            .BuildServiceProvider();

        var client = new ChatClientBuilder(innerClient)
            .UseIndexThinking(options =>
            {
                options.EnableReasoning = true;
                options.AutoDetectReasoningRequirement = false;
            })
            .Build(services);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Think about this")
        };

        // Act - should not throw
        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in client.GetStreamingResponseAsync(messages))
        {
            updates.Add(update);
        }

        // Assert - should have metadata
        updates.Should().NotBeEmpty();
        var lastUpdate = updates.Last();
        lastUpdate.AdditionalProperties.Should().ContainKey(ThinkingChatClient.TurnResultKey);
    }
}
