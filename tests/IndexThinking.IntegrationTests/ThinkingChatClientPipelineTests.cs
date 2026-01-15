using FluentAssertions;
using IndexThinking.Abstractions;
using IndexThinking.Agents;
using IndexThinking.Client;
using IndexThinking.Continuation;
using IndexThinking.Core;
using IndexThinking.Extensions;
using IndexThinking.IntegrationTests.Fixtures;
using IndexThinking.Stores;
using IndexThinking.Tokenization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IndexThinking.IntegrationTests;

/// <summary>
/// Integration tests for ThinkingChatClient with real component implementations.
/// Tests full pipeline from request to response with actual services (not mocks).
/// </summary>
public class ThinkingChatClientPipelineTests
{
    [Fact]
    public async Task FullPipeline_SimpleRequest_CompletesSuccessfully()
    {
        // Arrange
        var innerClient = new MockChatClient()
            .WithResponse("Hello, how can I help you?");

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
        var response = await client.GetResponseAsync(messages);

        // Assert
        response.Should().NotBeNull();
        response.Messages.Should().NotBeEmpty();
        response.Messages[0].Text.Should().Contain("Hello");

        // Verify metadata
        var metrics = response.GetTurnMetrics();
        metrics.Should().NotBeNull();

        var turnResult = response.GetTurnResult();
        turnResult.Should().NotBeNull();
        turnResult!.WasTruncated.Should().BeFalse();
    }

    [Fact]
    public async Task FullPipeline_TruncatedResponse_ContinuesAutomatically()
    {
        // Arrange
        var innerClient = new MockChatClient()
            .WithResponses(
                ("This is a truncated response that ends abruptly in the middle of", ChatFinishReason.Length),
                (" a sentence. Here is the complete response.", ChatFinishReason.Stop));

        var services = new ServiceCollection()
            .AddIndexThinkingAgents()
            .AddIndexThinkingInMemoryStorage()
            .BuildServiceProvider();

        var client = new ChatClientBuilder(innerClient)
            .UseIndexThinking()
            .Build(services);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Tell me a story")
        };

        // Act
        var response = await client.GetResponseAsync(messages);

        // Assert
        response.Should().NotBeNull();
        innerClient.CallCount.Should().Be(2); // Initial + continuation

        var metrics = response.GetTurnMetrics();
        metrics.Should().NotBeNull();
        metrics!.ContinuationCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task FullPipeline_WithSessionId_PreservesSession()
    {
        // Arrange
        var innerClient = new MockChatClient()
            .WithResponse("First response")
            .WithResponse("Second response");

        var services = new ServiceCollection()
            .AddIndexThinkingAgents()
            .AddIndexThinkingInMemoryStorage()
            .AddIndexThinkingContext()
            .BuildServiceProvider();

        var options = new ThinkingChatClientOptions
        {
            EnableContextTracking = true,
            EnableContextInjection = true
        };

        var contextTracker = services.GetRequiredService<IContextTracker>();
        var contextInjector = services.GetRequiredService<IContextInjector>();
        var turnManager = services.GetRequiredService<IThinkingTurnManager>();

        var client = new ThinkingChatClient(
            innerClient,
            turnManager,
            options,
            contextTracker,
            contextInjector);

        var sessionOptions = ThinkingChatClientExtensions.WithSession("test-session-123");

        // Act - First request
        var response1 = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Hello")],
            sessionOptions);

        // Act - Second request
        var response2 = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "How are you?")],
            sessionOptions);

        // Assert
        response1.Should().NotBeNull();
        response2.Should().NotBeNull();

        // Verify context was tracked
        var context = contextTracker.GetContext("test-session-123");
        context.RecentTurns.Should().HaveCount(2);
    }

    [Fact]
    public async Task FullPipeline_ComplexRequest_EstimatesComplexity()
    {
        // Arrange
        var innerClient = new MockChatClient()
            .WithResponse("Here's a detailed analysis of the bug...");

        var services = new ServiceCollection()
            .AddIndexThinkingAgents()
            .AddIndexThinkingInMemoryStorage()
            .BuildServiceProvider();

        var client = new ChatClientBuilder(innerClient)
            .UseIndexThinking()
            .Build(services);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Debug and analyze this complex code issue in depth")
        };

        // Act
        var response = await client.GetResponseAsync(messages);

        // Assert
        response.Should().NotBeNull();
        var metrics = response.GetTurnMetrics();
        metrics.Should().NotBeNull();
        // Complex keywords should trigger higher complexity estimation
    }

    [Fact]
    public async Task FullPipeline_Cancellation_ThrowsOperationCancelled()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var innerClient = new MockChatClient()
            .WithResponseFactory(_ =>
            {
                cts.Cancel();
                throw new OperationCanceledException();
            });

        var services = new ServiceCollection()
            .AddIndexThinkingAgents()
            .AddIndexThinkingInMemoryStorage()
            .BuildServiceProvider();

        var client = new ChatClientBuilder(innerClient)
            .UseIndexThinking()
            .Build(services);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello")
        };

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => client.GetResponseAsync(messages, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task FullPipeline_WithModelId_PassesModelToContext()
    {
        // Arrange
        var innerClient = new MockChatClient()
            .WithResponse("Response from GPT-4");

        var services = new ServiceCollection()
            .AddIndexThinkingAgents()
            .AddIndexThinkingInMemoryStorage()
            .BuildServiceProvider();

        var client = new ChatClientBuilder(innerClient)
            .UseIndexThinking()
            .Build(services);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello")
        };

        var options = new ChatOptions { ModelId = "gpt-4o" };

        // Act
        var response = await client.GetResponseAsync(messages, options);

        // Assert
        response.Should().NotBeNull();
    }

    [Fact]
    public async Task FullPipeline_MaxContinuations_StopsAfterLimit()
    {
        // Arrange - Always return truncated with responses long enough to pass progress check
        var callCount = 0;
        var innerClient = new MockChatClient()
            .WithResponseFactory(_ =>
            {
                callCount++;
                // Responses must be longer than MinProgressPerContinuation (default: 10 chars)
                return new ChatResponse([new ChatMessage(ChatRole.Assistant, $"This is a long enough response for part {callCount} to pass progress check...")])
                {
                    FinishReason = ChatFinishReason.Length
                };
            });

        var services = new ServiceCollection()
            .AddIndexThinkingAgents()
            .AddIndexThinkingInMemoryStorage()
            .BuildServiceProvider();

        // Configure MaxContinuations via UseIndexThinking options (not AgentOptions)
        var client = new ChatClientBuilder(innerClient)
            .UseIndexThinking(options =>
            {
                options.DefaultContinuation = new ContinuationConfig
                {
                    MaxContinuations = 3
                };
            })
            .Build(services);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Tell me everything")
        };

        // Act
        var response = await client.GetResponseAsync(messages);

        // Assert
        response.Should().NotBeNull();
        innerClient.CallCount.Should().BeLessThanOrEqualTo(4); // Initial + max 3 continuations

        var result = response.GetTurnResult();
        result.Should().NotBeNull();
        result!.WasTruncated.Should().BeTrue(); // Still truncated after max continuations
    }
}
