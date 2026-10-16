using System.Text.Json;
using AwesomeAssertions;
using IndexThinking.Abstractions;
using IndexThinking.Tokenization;
using Microsoft.Extensions.AI;
using Xunit;

namespace IndexThinking.Tests.Tokenization;

/// <summary>
/// A message's count covers everything it sends: text, tool calls with their arguments, tool results, reasoning and images -
/// the same way in the approximate and the exact counter. And the approximate estimate stays near o200k on the kinds of text
/// a conversation carries (before, Korean was −25 %, JSON −33 % and a tool result counted as an empty message).
/// </summary>
public class MessageContentCountingTests
{
    private const string Json = """{"files":[{"path":"src/app/Program.cs","size":1834,"modified":"2026-10-16T08:15:00Z"},{"path":"README.md","size":912,"modified":"2026-10-15T21:02:11Z"}],"total":2,"truncated":false}""";
    private const string Korean = "지난주 회의에서 결정한 배포 일정은 다음 주 화요일로 미뤄졌고, 테스트 환경의 데이터베이스 이전이 끝나야 운영 반영을 시작할 수 있다고 합니다.";

    public static TheoryData<string> Counters => ["approximate", "o200k"];

    private static IChatMessageTokenCounter Counter(string name) =>
        name == "o200k" ? new TiktokenTokenCounter("o200k_base") : new ApproximateTokenCounter();

    [Theory]
    [MemberData(nameof(Counters))]
    public void AToolResult_CountsItsPayload(string name)
    {
        var counter = Counter(name);
        var message = new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call-1", Json)]);

        counter.Count(message).Should().BeGreaterThanOrEqualTo(counter.Count(Json));
    }

    [Theory]
    [MemberData(nameof(Counters))]
    public void AToolResultAsJsonElement_CountsLikeItsText(string name)
    {
        var counter = Counter(name);
        var asText = counter.Count(new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call-1", Json)]));
        var asElement = counter.Count(new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call-1", JsonDocument.Parse(Json).RootElement)]));

        asElement.Should().Be(asText);
    }

    [Theory]
    [MemberData(nameof(Counters))]
    public void AToolCall_CountsItsNameAndArguments(string name)
    {
        var counter = Counter(name);
        var call = new FunctionCallContent("call-1", "write_file", new Dictionary<string, object?> { ["path"] = "notes.json", ["content"] = Json });
        var message = new ChatMessage(ChatRole.Assistant, [call]);

        counter.Count(message).Should().BeGreaterThan(counter.Count(Json) + counter.Count("write_file"));
    }

    [Theory]
    [MemberData(nameof(Counters))]
    public void ReasoningAndText_AreBothCounted(string name)
    {
        var counter = Counter(name);
        var both = new ChatMessage(ChatRole.Assistant, [new TextReasoningContent("First check the dates."), new TextContent(Korean)]);
        var textOnly = new ChatMessage(ChatRole.Assistant, [new TextContent(Korean)]);

        counter.Count(both).Should().Be(counter.Count(textOnly) + counter.Count("First check the dates."));
    }

    [Theory]
    [MemberData(nameof(Counters))]
    public void AnImage_HasAFixedCost(string name)
    {
        var counter = Counter(name);
        var image = new ChatMessage(ChatRole.User, [new DataContent(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, "image/png")]);
        var empty = new ChatMessage(ChatRole.User, []);

        (counter.Count(image) - counter.Count(empty)).Should().Be(ApproximateTokenCounter.ImageTokens);
    }

    [Theory]
    [MemberData(nameof(Counters))]
    public void AToolResultMadeOfContent_CountsItsTextAndItsImages(string name)
    {
        // A screenshot or MCP tool returns content, not a string: its text counts as text, each image once
        var counter = Counter(name);
        var parts = new List<AIContent> { new TextContent(Korean), new DataContent(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, "image/png") };
        var message = new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call-1", parts)]);
        var textOnly = new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call-1", Korean)]);

        (counter.Count(message) - counter.Count(textOnly)).Should().Be(ApproximateTokenCounter.ImageTokens);
    }

    [Fact]
    public void AnAgentHistory_FitsTheOutputCapMargin()
    {
        // The output cap is MaxContextTokens - 1.15 x estimated input - 128: it keeps the request inside the window only while
        // 1.15 x estimate + 128 covers the real input. A Korean conversation with JSON tool rounds, the shape that broke it
        // (tool messages counted as empty).
        var history = new List<ChatMessage> { new(ChatRole.System, "You are a file assistant. Answer in Korean."), new(ChatRole.User, Korean) };
        for (var i = 0; i < 8; i++)
        {
            history.Add(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent($"call-{i}", "list_files", new Dictionary<string, object?> { ["path"] = $"src/app/module{i}" })]));
            history.Add(new ChatMessage(ChatRole.Tool, [new FunctionResultContent($"call-{i}", Json)]));
            history.Add(new ChatMessage(ChatRole.Assistant, Korean));
        }

        var approximate = new ApproximateTokenCounter();
        var o200k = new TiktokenTokenCounter("o200k_base");
        var estimate = history.Sum(approximate.Count);
        var real = history.Sum(o200k.Count);

        (estimate * 1.15 + 128).Should().BeGreaterThanOrEqualTo(real, $"estimate {estimate} vs o200k {real}");
    }

    [Theory]
    [InlineData(Korean)]
    [InlineData(Json)]
    [InlineData("The deployment was moved to next Tuesday; the database migration in the test environment has to finish first.")]
    [InlineData("public sealed record CachedModelInfo(string Id, long SizeBytes, bool IsOwnSnapshot);\n    var total = items.Sum(i => i.SizeBytes);")]
    public void TheApproximateEstimate_DoesNotUndercountPastTheOutputCapMargin(string text)
    {
        // One text varies (common words merge); over a corpus the estimate is within a few percent (bench/context). What a
        // single request must not do is undercount by more than the 15 % input margin the output cap leaves.
        var estimate = new ApproximateTokenCounter().Count(text);
        var real = new TiktokenTokenCounter("o200k_base").Count(text);

        ((double)estimate / real).Should().BeInRange(0.87, 1.4, $"estimate {estimate} vs o200k {real}");
    }
}
