using System.Runtime.CompilerServices;
using FluentAssertions;
using IndexThinking.Client;
using IndexThinking.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IndexThinking.IntegrationTests;

/// <summary>
/// MU-3 integration tests: opt-in live separation of inline <c>&lt;think&gt;…&lt;/think&gt;</c> reasoning from
/// answer text during streaming, including tags split across chunk boundaries. Exercised through the public
/// <see cref="ThinkingChatClient.GetStreamingResponseAsync"/> path with a chunk-controlling inner client.
/// </summary>
public class StreamingReasoningSeparationTests
{
    /// <summary>Inner client that streams exactly the caller-specified chunks (one TextContent update each).</summary>
    private sealed class ChunkChatClient(params string[] chunks) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
            => Task.FromResult(new ChatResponse([new ChatMessage(ChatRole.Assistant, string.Concat(chunks))]) { FinishReason = ChatFinishReason.Stop });

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken ct = default)
        {
            for (var i = 0; i < chunks.Length; i++)
            {
                var update = new ChatResponseUpdate { Contents = [new TextContent(chunks[i])] };
                if (i == chunks.Length - 1)
                    update.FinishReason = ChatFinishReason.Stop;
                yield return update;
                await Task.Yield();
            }
        }

        public void Dispose() { }
        public object? GetService(Type serviceType, object? serviceKey = null) => serviceType == typeof(IChatClient) ? this : null;
    }

    private static IChatClient BuildClient(IChatClient inner, bool separate)
    {
        var services = new ServiceCollection()
            .AddIndexThinkingAgents()
            .AddIndexThinkingInMemoryStorage()
            .BuildServiceProvider();

        return new ChatClientBuilder(inner)
            .UseIndexThinking(o => o.SeparateReasoningInStream = separate)
            .Build(services);
    }

    private static async Task<(string Answer, string Reasoning)> CollectAsync(IChatClient client)
    {
        var answer = new System.Text.StringBuilder();
        var reasoning = new System.Text.StringBuilder();
        await foreach (var update in client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "hi")]))
        {
            foreach (var content in update.Contents)
            {
                switch (content)
                {
                    case TextReasoningContent rc: reasoning.Append(rc.Text); break;
                    case TextContent tc: answer.Append(tc.Text); break;
                }
            }
        }
        return (answer.ToString(), reasoning.ToString());
    }

    [Fact]
    public async Task Separation_On_SplitsThinkTags_AcrossChunkBoundaries()
    {
        // The <think> open and </think> close tags are deliberately split across chunk boundaries.
        var inner = new ChunkChatClient("Hello <thi", "nk>rea", "soning</thi", "nk> world");
        var client = BuildClient(inner, separate: true);

        var (answer, reasoning) = await CollectAsync(client);

        reasoning.Should().Be("reasoning");
        answer.Should().Be("Hello  world");
    }

    [Fact]
    public async Task Separation_Off_PassesRawTagsThrough()
    {
        var inner = new ChunkChatClient("Hello <think>reasoning</think> world");
        var client = BuildClient(inner, separate: false);

        var (answer, reasoning) = await CollectAsync(client);

        reasoning.Should().BeEmpty("default behavior is raw pass-through (zero regression)");
        answer.Should().Contain("<think>reasoning</think>");
    }

    [Fact]
    public async Task Separation_On_HandlesMultipleThinkBlocks()
    {
        var inner = new ChunkChatClient("a<think>r1</think>b<think>r2</think>c");
        var client = BuildClient(inner, separate: true);

        var (answer, reasoning) = await CollectAsync(client);

        reasoning.Should().Be("r1r2");
        answer.Should().Be("abc");
    }

    [Fact]
    public async Task Separation_On_NativeReasoningContent_PassesThroughWithoutDoubleClassification()
    {
        // A native provider emits a separate reasoning channel (no inline tags). Enabling separation must
        // not reclassify it: reasoning stays reasoning, answer stays answer.
        var inner = new NativeReasoningClient();
        var client = BuildClient(inner, separate: true);

        var (answer, reasoning) = await CollectAsync(client);

        reasoning.Should().Be("native-reasoning");
        answer.Should().Be("answer");
    }

    private sealed class NativeReasoningClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
            => Task.FromResult(new ChatResponse([new ChatMessage(ChatRole.Assistant, "answer")]) { FinishReason = ChatFinishReason.Stop });

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken ct = default)
        {
            yield return new ChatResponseUpdate { Contents = [new TextReasoningContent("native-reasoning")] };
            yield return new ChatResponseUpdate { Contents = [new TextContent("answer")], FinishReason = ChatFinishReason.Stop };
            await Task.Yield();
        }

        public void Dispose() { }
        public object? GetService(Type serviceType, object? serviceKey = null) => serviceType == typeof(IChatClient) ? this : null;
    }
}
