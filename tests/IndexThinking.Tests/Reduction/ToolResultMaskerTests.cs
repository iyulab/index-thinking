using AwesomeAssertions;
using IndexThinking.Reduction;
using Microsoft.Extensions.AI;
using Xunit;

namespace IndexThinking.Tests.Reduction;

/// <summary>
/// <see cref="ToolResultMasker"/> masks old tool results (outside the newest user turns, or past a token budget) with a
/// placeholder naming the call; with a retention it names the id to read the original back by. Over the budget, the
/// reducer masks before it drops anything.
/// </summary>
public class ToolResultMaskerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly string Page = string.Join(' ', Enumerable.Repeat("the quick brown fox jumps over the lazy dog", 40));

    /// <summary><paramref name="turns"/> user turns, each with one tool round returning a page.</summary>
    private static List<ChatMessage> Turns(int turns)
    {
        var messages = new List<ChatMessage>();
        for (var t = 1; t <= turns; t++)
        {
            messages.Add(new ChatMessage(ChatRole.User, $"question {t}"));
            messages.Add(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent($"c{t}", "send_report",
                new Dictionary<string, object?> { ["section"] = t })]));
            messages.Add(new ChatMessage(ChatRole.Tool, [new FunctionResultContent($"c{t}", $"section {t}: {Page}")]));
            messages.Add(new ChatMessage(ChatRole.Assistant, $"answer {t}"));
        }

        return messages;
    }

    private static string ResultOf(IReadOnlyList<ChatMessage> messages, string callId) =>
        messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>().Single(r => r.CallId == callId).Result!.ToString()!;

    [Fact]
    public void ResultsOutsideTheNewestTurns_AreMasked_NamingTheCall()
    {
        var masked = new ToolResultMasker(protectedTurns: 3).Mask(Turns(5));

        ResultOf(masked, "c1").Should().StartWith("[Masked: send_report {\"section\":1} result,")
            .And.Contain("call send_report again with the same arguments");
        ResultOf(masked, "c2").Should().StartWith("[Masked:");
        ResultOf(masked, "c3").Should().StartWith("section 3:");
        ResultOf(masked, "c5").Should().StartWith("section 5:");
    }

    [Fact]
    public void WithARetention_ThePlaceholderNamesTheIdAndTheOriginalIsKept()
    {
        var retention = new Retention();

        var masked = new ToolResultMasker(protectedTurns: 3).Mask(Turns(5), retention);

        ResultOf(masked, "c1").Should().Contain("call retrieve_tool_result with id \"c1\" to read it again (do not repeat the call)");
        retention.Kept["c1"].Should().Be($"section 1: {Page}");
    }

    [Fact]
    public void ATokenBudget_MasksInsideOneTurn_ButNeverTheNewestRound()
    {
        var messages = new List<ChatMessage> { new(ChatRole.User, "Read everything.") };
        for (var r = 1; r <= 4; r++)
        {
            messages.Add(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent($"r{r}", "read")]));
            messages.Add(new ChatMessage(ChatRole.Tool, [new FunctionResultContent($"r{r}", $"part {r}: {Page}")]));
        }

        var masked = new ToolResultMasker(protectedTurns: 3, protectedTokens: 1).Mask(messages);

        ResultOf(masked, "r1").Should().StartWith("[Masked:");
        ResultOf(masked, "r3").Should().StartWith("[Masked:");
        ResultOf(masked, "r4").Should().StartWith("part 4:");
    }

    [Fact]
    public void NothingToMask_ReturnsTheSameList()
    {
        var messages = Turns(2);

        new ToolResultMasker(protectedTurns: 3).Mask(messages).Should().BeSameAs(messages);
    }

    [Fact]
    public async Task OverTheBudget_TheReducerMasksBeforeItDrops()
    {
        var messages = Turns(6);
        var budget = new IndexThinking.Tokenization.ApproximateTokenCounter();
        var masked = new ToolResultMasker().Mask(messages);
        var maskedTokens = masked.Sum(m => budget.Count(m));

        var reduced = (await new ContextBudgetChatReducer(new ContextBudgetOptions { MaxInputTokens = maskedTokens + 10 })
            .ReduceAsync(messages, Ct)).ToList();

        reduced.Should().HaveCount(messages.Count, "masking alone fits the budget, so nothing is dropped");
        ResultOf(reduced, "c1").Should().StartWith("[Masked:");
        reduced.Should().NotContain(m => m.Text.Contains("earlier messages omitted"));
    }

    private sealed class Retention : IToolResultRetention
    {
        public Dictionary<string, string> Kept { get; } = [];

        public string ToolName => "retrieve_tool_result";

        public string Retain(FunctionResultContent result, string text)
        {
            Kept[result.CallId] = text;
            return result.CallId;
        }
    }
}
