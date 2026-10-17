using AwesomeAssertions;
using IndexThinking.Reduction;
using Microsoft.Extensions.AI;
using Xunit;

namespace IndexThinking.Tests.Reduction;

/// <summary>
/// <see cref="ContextBudgetChatReducer"/> fits a request into a budget without breaking a tool round, keeps the system
/// messages, the last user message and the newest round, and marks where it dropped messages.
/// </summary>
public class ContextBudgetChatReducerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly string Page = string.Join(' ', Enumerable.Repeat("the quick brown fox jumps over the lazy dog", 40));

    /// <summary>A system prompt, one user request, then <paramref name="rounds"/> tool rounds each returning a page.</summary>
    private static List<ChatMessage> OneTaskWithRounds(int rounds)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a careful reader."),
            new(ChatRole.User, "Read every section and summarize the report."),
        };
        for (var r = 1; r <= rounds; r++)
        {
            messages.Add(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent($"c{r}", "read_section")]));
            messages.Add(new ChatMessage(ChatRole.Tool, [new FunctionResultContent($"c{r}", $"section {r}: {Page}")]));
        }

        return messages;
    }

    private static async Task<List<ChatMessage>> ReduceAsync(IEnumerable<ChatMessage> messages, int maxTokens, ToolResultTrimmer? trimmer = null) =>
        [.. await new ContextBudgetChatReducer(new ContextBudgetOptions { MaxInputTokens = maxTokens, ToolResults = trimmer ?? new ToolResultTrimmer() })
            .ReduceAsync(messages, Ct)];

    private static void AssertEveryCallHasItsResultAndEveryResultItsCall(IReadOnlyList<ChatMessage> request)
    {
        var calls = request.SelectMany(m => m.Contents).OfType<FunctionCallContent>().Select(c => c.CallId).ToHashSet();
        var results = request.SelectMany(m => m.Contents).OfType<FunctionResultContent>().Select(r => r.CallId).ToHashSet();
        results.Should().BeEquivalentTo(calls);
    }

    [Fact]
    public async Task UnderTheBudget_TheRequestIsUnchanged()
    {
        var messages = OneTaskWithRounds(2);

        var reduced = await ReduceAsync(messages, 100_000);

        reduced.Should().Equal(messages);
    }

    [Fact]
    public async Task OverTheBudget_OldRoundsLeaveWhole_AndTheTaskAndTheNewestRoundStay()
    {
        var messages = OneTaskWithRounds(12);

        var reduced = await ReduceAsync(messages, 1_500);

        AssertEveryCallHasItsResultAndEveryResultItsCall(reduced);
        reduced[0].Role.Should().Be(ChatRole.System);
        reduced.Should().Contain(m => m.Text == "Read every section and summarize the report.");
        reduced.SelectMany(m => m.Contents).OfType<FunctionCallContent>().Select(c => c.CallId).Should().Contain("c12")
            .And.NotContain("c1");
        reduced.Should().Contain(m => m.Role == ChatRole.User && m.Text.Contains("earlier messages omitted"));
        reduced.Count.Should().BeLessThan(messages.Count);
    }

    [Fact]
    public async Task TheReducedRequest_FitsTheBudget()
    {
        var counter = new IndexThinking.Tokenization.ApproximateTokenCounter();

        var reduced = await ReduceAsync(OneTaskWithRounds(12), 1_500);

        reduced.Sum(m => counter.Count(m)).Should().BeLessThanOrEqualTo(1_500);
    }

    [Fact]
    public async Task ADroppedRunIsMarkedWhereItWas()
    {
        // History: an earlier exchange, then the current task and its rounds. Both the earlier exchange and the oldest
        // rounds of the current task go; each run gets its own note, and the task stays between them.
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "sys"),
            new(ChatRole.User, $"An earlier question. {Page}"),
            new(ChatRole.Assistant, $"An earlier answer. {Page}"),
        };
        messages.AddRange(OneTaskWithRounds(8).Skip(1));

        var reduced = await ReduceAsync(messages, 1_200);

        var texts = reduced.Select(m => m.Text).ToList();
        texts[1].Should().Contain("2 earlier messages omitted");
        texts[2].Should().Be("Read every section and summarize the report.");
        texts[3].Should().Contain("earlier messages omitted");
        AssertEveryCallHasItsResultAndEveryResultItsCall(reduced);
    }

    [Fact]
    public async Task AToolResultOverTheLimit_KeepsHeadAndTail()
    {
        var long_ = string.Join('\n', Enumerable.Range(0, 2_000).Select(i => $"line {i}"));
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Show the log."),
            new(ChatRole.Assistant, [new FunctionCallContent("a", "read_log")]),
            new(ChatRole.Tool, [new FunctionResultContent("a", long_)]),
        };

        var reduced = await ReduceAsync(messages, 1_000_000, new ToolResultTrimmer(maxChars: 2_000, keepHeadLines: 5, keepTailLines: 3));

        var text = reduced[2].Contents.OfType<FunctionResultContent>().Single().Result!.ToString()!;
        text.Should().StartWith("line 0\nline 1").And.EndWith("line 1997\nline 1998\nline 1999").And.Contain("[... 1,992 lines omitted");
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void TheTrimmer_KeepsASmallResultWhole_AndCutsAOneLineResultToFitTheLimit(int length, bool whole)
    {
        var text = new string('x', length);
        var trimmed = new ToolResultTrimmer(maxChars: 100).Trim(text);

        if (whole)
        {
            trimmed.Should().BeSameAs(text);
        }
        else
        {
            // Marker included, the result fits the limit — trimming it again changes nothing
            trimmed.Length.Should().BeLessThanOrEqualTo(100);
            trimmed.Should().StartWith("xxxxxxxxxx").And.EndWith("[... truncated (101 chars total) ...]");
        }
    }

    [Fact]
    public async Task WorksAsAnMeaiReducer_InFrontOfAClient()
    {
        // Positive control for the wiring: the inner client sees the reduced request.
        IList<ChatMessage>? seen = null;
        var inner = new TestChatClient((messages, _) => { seen = [.. messages]; return new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")); });
        var client = new ChatClientBuilder(inner)
            .UseChatReducer(new ContextBudgetChatReducer(new ContextBudgetOptions { MaxInputTokens = 1_500 }))
            .Build();

        await client.GetResponseAsync(OneTaskWithRounds(12), cancellationToken: Ct);

        seen.Should().NotBeNull();
        seen!.Count.Should().BeLessThan(OneTaskWithRounds(12).Count);
        AssertEveryCallHasItsResultAndEveryResultItsCall([.. seen]);
    }

    private sealed class TestChatClient(Func<IEnumerable<ChatMessage>, ChatOptions?, ChatResponse> respond) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => Task.FromResult(respond(messages, options));

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
