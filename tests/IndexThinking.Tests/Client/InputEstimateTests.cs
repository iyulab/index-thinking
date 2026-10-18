using AwesomeAssertions;
using IndexThinking.Abstractions;
using IndexThinking.Agents;
using IndexThinking.Client;
using IndexThinking.Continuation;
using IndexThinking.Tokenization;
using Microsoft.Extensions.AI;
using NSubstitute;
using Xunit;

namespace IndexThinking.Tests.Client;

/// <summary>
/// The client's input estimate - for the context check and the output cap - covers the tool definitions a request offers,
/// and learns its factor from what the provider reports, so a model whose tokenizer is not o200k is not undercounted for
/// long.
/// </summary>
public class InputEstimateTests
{
    private const int Window = 8000;

    private static (ThinkingChatClient Client, List<ChatOptions?> Seen, IChatClient Inner) Build(Func<IList<ChatMessage>, long?> reportedInput, bool calibrate = true)
    {
        var inner = Substitute.For<IChatClient>();
        var seen = new List<ChatOptions?>();
        inner.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Do<ChatOptions?>(o => seen.Add(o)), Arg.Any<CancellationToken>())
            .Returns(call => new ChatResponse([new ChatMessage(ChatRole.Assistant, "ok")])
            {
                Usage = reportedInput(call.ArgAt<IEnumerable<ChatMessage>>(0).ToList()) is { } input ? new UsageDetails { InputTokenCount = input } : null,
            });
        var turns = Substitute.For<IThinkingTurnManager>();
        turns.ProcessTurnAsync(Arg.Any<ThinkingContext>(), Arg.Any<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>())
            .Returns(async call =>
            {
                var send = call.ArgAt<Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>>>(1);
                var response = await send(call.ArgAt<ThinkingContext>(0).Messages.ToList(), CancellationToken.None);
                return TurnResult.Success(response, TurnMetrics.CreateBuilder().Build());
            });
        var options = new ThinkingChatClientOptions
        {
            DefaultContinuation = new ContinuationConfig { MaxContextTokens = Window },
            CalibrateTokenCounts = calibrate,
        };
        return (new ThinkingChatClient(inner, turns, options, tokenCounter: new ApproximateTokenCounter()), seen, inner);
    }

    private static List<ChatMessage> Conversation() =>
        [new(ChatRole.User, string.Join(' ', Enumerable.Repeat("Summarise the attached quarterly report for the board.", 40)))];

    [Fact]
    public async Task ToolDefinitions_CountTowardTheOutputCap()
    {
        var (client, seen, _) = Build(_ => null);
        var schemaHeavy = AIFunctionFactory.Create(
            (string path, string content, string encoding, bool overwrite, int retries) => "ok",
            "write_file",
            string.Join(' ', Enumerable.Repeat("Writes a file at the given path, creating folders as needed.", 30)));

        await client.GetResponseAsync(Conversation(), new ChatOptions { MaxOutputTokens = Window }, TestContext.Current.CancellationToken);
        await client.GetResponseAsync(Conversation(), new ChatOptions { MaxOutputTokens = Window, Tools = [schemaHeavy] }, TestContext.Current.CancellationToken);

        seen[1]!.MaxOutputTokens.Should().BeLessThan(seen[0]!.MaxOutputTokens!.Value, "the tool's name, description and schema travel with the request");
    }

    [Fact]
    public async Task TheProvidersReportedInput_CalibratesLaterEstimates()
    {
        // A tokenizer that needs twice what the o200k-fitted estimate says (a local model on Korean, roughly)
        var raw = new ApproximateTokenCounter();
        var (client, seen, _) = Build(messages => 2L * messages.Sum(raw.Count));

        await client.GetResponseAsync(Conversation(), new ChatOptions { MaxOutputTokens = Window }, TestContext.Current.CancellationToken);
        var firstCap = seen[0]!.MaxOutputTokens!.Value;
        await client.GetResponseAsync(Conversation(), new ChatOptions { MaxOutputTokens = Window }, TestContext.Current.CancellationToken);

        client.TokenEstimateFactor.Should().BeApproximately(2.0, 0.01);
        seen[1]!.MaxOutputTokens!.Value.Should().BeLessThan(firstCap, "the second request is estimated at the provider's count");
        var real = 2 * Conversation().Sum(raw.Count);
        (real + seen[1]!.MaxOutputTokens!.Value).Should().BeLessThanOrEqualTo(Window, "the capped request fits the window by the provider's own count");
    }

    [Fact]
    public async Task WithCalibrationOff_TheFactorStaysOne()
    {
        var raw = new ApproximateTokenCounter();
        var (client, _, _) = Build(messages => 2L * messages.Sum(raw.Count), calibrate: false);

        await client.GetResponseAsync(Conversation(), new ChatOptions { MaxOutputTokens = Window }, TestContext.Current.CancellationToken);

        client.TokenEstimateFactor.Should().Be(1.0);
    }

    [Fact]
    public void Calibration_IgnoresSmallRequests_AndStaysWithinItsBounds()
    {
        var calibration = new TokenEstimateCalibration();

        calibration.Observe(estimated: 50, actual: 500);
        calibration.Factor.Should().Be(1.0, "a request this small is mostly the provider's own template tokens");

        calibration.Observe(estimated: 1000, actual: 100_000);
        calibration.Factor.Should().Be(TokenEstimateCalibration.MaxFactor);
    }
}
