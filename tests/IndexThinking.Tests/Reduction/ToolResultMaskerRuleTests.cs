using IndexThinking.Abstractions;
using IndexThinking.Reduction;
using Microsoft.Extensions.AI;
using Xunit;

namespace IndexThinking.Tests.Reduction;

/// <summary>
/// The masking rule of <see cref="ToolResultMasker"/> — old tool results replaced with compact placeholders (these tests
/// moved here from IronHive.Agent with the rule).
/// </summary>
public class ToolResultMaskerRuleTests
{
    #region Basic Masking

    [Fact]
    public void MaskObservations_EmptyHistory_ReturnsEmpty()
    {
        var masker = new ToolResultMasker();
        var result = masker.Mask([]);
        Assert.Empty(result);
    }

    [Fact]
    public void MaskObservations_NoToolMessages_ReturnsOriginal()
    {
        var masker = new ToolResultMasker(protectedTurns: 1);
        var history = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello"),
            new(ChatRole.Assistant, "Hi there"),
            new(ChatRole.User, "How are you?"),
            new(ChatRole.Assistant, "I'm fine"),
        };

        var result = masker.Mask(history);
        Assert.Same(history, result); // Same reference — nothing changed
    }

    [Fact]
    public void MaskObservations_OldLargeToolResult_IsMasked()
    {
        var masker = new ToolResultMasker(protectedTurns: 1, minimumResultLength: 50);

        var largeResult = new string('x', 500);
        var history = new List<ChatMessage>
        {
            new(ChatRole.User, "Read the file"),
            CreateAssistantWithToolCall("call-1", "read_file"),
            CreateToolResult("call-1", largeResult),
            new(ChatRole.User, "What did you find?"),
            new(ChatRole.Assistant, "I found some data"),
            // Protected turn below
            new(ChatRole.User, "Thanks"),
            new(ChatRole.Assistant, "You're welcome"),
        };

        var result = masker.Mask(history);

        // The tool result message should be masked
        var toolMsg = result[2];
        Assert.Equal(ChatRole.Tool, toolMsg.Role);
        var frc = toolMsg.Contents.OfType<FunctionResultContent>().Single();
        var resultText = frc.Result?.ToString()!;
        Assert.Contains("[Masked:", resultText);
        Assert.Contains("read_file", resultText);
        Assert.Contains("500", resultText);
    }

    [Fact]
    public void MaskObservations_SmallToolResult_NotMasked()
    {
        var masker = new ToolResultMasker(protectedTurns: 1, minimumResultLength: 200);

        var smallResult = "OK";
        var history = new List<ChatMessage>
        {
            new(ChatRole.User, "Do something"),
            CreateAssistantWithToolCall("call-1", "some_tool"),
            CreateToolResult("call-1", smallResult),
            // Protected turn
            new(ChatRole.User, "Done?"),
            new(ChatRole.Assistant, "Yes"),
        };

        var result = masker.Mask(history);

        // Small result should NOT be masked
        var toolMsg = result[2];
        var frc = toolMsg.Contents.OfType<FunctionResultContent>().Single();
        Assert.Equal("OK", frc.Result?.ToString());
    }

    #endregion

    #region Turn Protection

    [Fact]
    public void MaskObservations_RecentTurnsProtected()
    {
        var masker = new ToolResultMasker(protectedTurns: 2, minimumResultLength: 50);

        var largeResult = new string('y', 300);
        var history = new List<ChatMessage>
        {
            // Old turn (should be masked)
            new(ChatRole.User, "Old request"),
            CreateAssistantWithToolCall("call-old", "read_file"),
            CreateToolResult("call-old", largeResult),
            // Protected turn 1
            new(ChatRole.User, "Recent request 1"),
            CreateAssistantWithToolCall("call-1", "read_file"),
            CreateToolResult("call-1", largeResult),
            // Protected turn 2
            new(ChatRole.User, "Recent request 2"),
            CreateAssistantWithToolCall("call-2", "grep"),
            CreateToolResult("call-2", largeResult),
        };

        var result = masker.Mask(history);

        // Old tool result (index 2) should be masked
        var oldToolFrc = result[2].Contents.OfType<FunctionResultContent>().Single();
        Assert.Contains("[Masked:", oldToolFrc.Result?.ToString());

        // Recent tool results (index 5, 8) should NOT be masked
        var recent1Frc = result[5].Contents.OfType<FunctionResultContent>().Single();
        Assert.Equal(largeResult, recent1Frc.Result?.ToString());

        var recent2Frc = result[8].Contents.OfType<FunctionResultContent>().Single();
        Assert.Equal(largeResult, recent2Frc.Result?.ToString());
    }

    [Fact]
    public void MaskObservations_FewerTurnsThanThreshold_NothingMasked()
    {
        var masker = new ToolResultMasker(protectedTurns: 5, minimumResultLength: 50);

        var largeResult = new string('z', 300);
        var history = new List<ChatMessage>
        {
            new(ChatRole.User, "Request 1"),
            CreateAssistantWithToolCall("call-1", "read_file"),
            CreateToolResult("call-1", largeResult),
            new(ChatRole.User, "Request 2"),
            new(ChatRole.Assistant, "Done"),
        };

        var result = masker.Mask(history);

        // Only 2 turns, threshold is 5 — everything is protected
        Assert.Same(history, result);
    }

    [Fact]
    public void MaskObservations_ZeroProtectedTurns_AllToolResultsMasked()
    {
        var masker = new ToolResultMasker(protectedTurns: 0, minimumResultLength: 50);

        var largeResult = new string('a', 300);
        var history = new List<ChatMessage>
        {
            new(ChatRole.User, "Request"),
            CreateAssistantWithToolCall("call-1", "tool_a"),
            CreateToolResult("call-1", largeResult),
        };

        var result = masker.Mask(history);

        // protectedTurns=0 means nothing is protected — all tool results should be masked
        Assert.NotSame(history, result);
        Assert.Equal(3, result.Count);

        // Tool result at index 2 should be masked
        var toolMsg = result[2];
        var frc = toolMsg.Contents.OfType<FunctionResultContent>().Single();
        var maskedText = frc.Result?.ToString() ?? "";
        Assert.Contains("[Masked:", maskedText);
    }

    [Fact]
    public void MaskObservations_ZeroProtectedTurns_MultipleUserMessages_AllMasked()
    {
        var masker = new ToolResultMasker(protectedTurns: 0, minimumResultLength: 50);

        var largeResult = new string('b', 300);
        var history = new List<ChatMessage>
        {
            new(ChatRole.User, "First request"),
            CreateAssistantWithToolCall("call-1", "tool_a"),
            CreateToolResult("call-1", largeResult),
            new(ChatRole.User, "Second request"),
            CreateAssistantWithToolCall("call-2", "tool_b"),
            CreateToolResult("call-2", largeResult),
        };

        var result = masker.Mask(history);

        // protectedTurns=0: both tool results should be masked
        Assert.NotSame(history, result);
        Assert.Equal(6, result.Count);

        // Both tool results should contain [Masked:
        var toolMsg1 = result[2];
        var frc1 = toolMsg1.Contents.OfType<FunctionResultContent>().Single();
        Assert.Contains("[Masked:", frc1.Result?.ToString() ?? "");

        var toolMsg2 = result[5];
        var frc2 = toolMsg2.Contents.OfType<FunctionResultContent>().Single();
        Assert.Contains("[Masked:", frc2.Result?.ToString() ?? "");
    }

    #endregion

    #region Placeholder Format

    [Fact]
    public void MaskObservations_PlaceholderContainsToolName()
    {
        var masker = new ToolResultMasker(protectedTurns: 1, minimumResultLength: 10);

        var result = ApplyMaskingToSingleToolResult(masker, "my_custom_tool", new string('x', 100));

        var frc = result.Contents.OfType<FunctionResultContent>().Single();
        Assert.Contains("my_custom_tool", frc.Result?.ToString());
    }

    [Fact]
    public void MaskObservations_PlaceholderContainsCharCount()
    {
        var masker = new ToolResultMasker(protectedTurns: 1, minimumResultLength: 10);

        var result = ApplyMaskingToSingleToolResult(masker, "tool", new string('x', 1234));

        var frc = result.Contents.OfType<FunctionResultContent>().Single();
        Assert.Contains("1,234", frc.Result?.ToString());
    }

    [Fact]
    public void MaskObservations_PlaceholderContainsLineCount()
    {
        var masker = new ToolResultMasker(protectedTurns: 1, minimumResultLength: 10);

        // 5 newlines → ~6 lines
        var multiline = string.Join("\n", Enumerable.Range(1, 6).Select(i => $"line {i}"));
        var result = ApplyMaskingToSingleToolResult(masker, "read_file", multiline);

        var frc = result.Contents.OfType<FunctionResultContent>().Single();
        Assert.Contains("~6 lines", frc.Result?.ToString());
    }

    [Fact]
    public void MaskObservations_PreservesCallId()
    {
        var masker = new ToolResultMasker(protectedTurns: 1, minimumResultLength: 10);

        var result = ApplyMaskingToSingleToolResult(masker, "tool", new string('x', 100), callId: "unique-call-42");

        var frc = result.Contents.OfType<FunctionResultContent>().Single();
        Assert.Equal("unique-call-42", frc.CallId);
    }

    #endregion

    #region Tool Name Resolution

    [Fact]
    public void MaskObservations_ResolvesToolNameFromAssistantMessage()
    {
        var masker = new ToolResultMasker(protectedTurns: 1, minimumResultLength: 10);

        // FunctionResultContent created without Name — name resolved from FunctionCallContent
        var history = new List<ChatMessage>
        {
            new(ChatRole.User, "Read it"),
            CreateAssistantWithToolCall("call-1", "special_reader"),
            CreateToolResult("call-1", new string('x', 200)),
            new(ChatRole.User, "Next"),
            new(ChatRole.Assistant, "OK"),
        };

        var result = masker.Mask(history);
        var frc = result[2].Contents.OfType<FunctionResultContent>().Single();
        Assert.Contains("special_reader", frc.Result?.ToString());
    }

    [Fact]
    public void MaskObservations_NoMatchingCallId_FallsBackToGenericName()
    {
        var masker = new ToolResultMasker(protectedTurns: 1, minimumResultLength: 10);

        // Tool result with no matching assistant message
        var history = new List<ChatMessage>
        {
            new(ChatRole.User, "Request"),
            CreateToolResult("orphan-call", new string('x', 200)),
            new(ChatRole.User, "Next"),
            new(ChatRole.Assistant, "OK"),
        };

        var result = masker.Mask(history);
        var frc = result[1].Contents.OfType<FunctionResultContent>().Single();
        Assert.Contains("tool", frc.Result?.ToString());
    }

    #endregion

    #region Multiple Tool Results

    [Fact]
    public void MaskObservations_MultipleResultsInOneMessage_MasksOnlyLarge()
    {
        var masker = new ToolResultMasker(protectedTurns: 1, minimumResultLength: 100);

        var toolMessage = new ChatMessage(ChatRole.Tool,
        [
            new FunctionResultContent("call-1", new string('x', 300)), // Large — mask
            new FunctionResultContent("call-2", "ok"),                  // Small — keep
        ]);

        var history = new List<ChatMessage>
        {
            new(ChatRole.User, "Do things"),
            CreateAssistantWithToolCall("call-1", "big_tool"),
            toolMessage,
            new(ChatRole.User, "Done"),
            new(ChatRole.Assistant, "Yes"),
        };

        var result = masker.Mask(history);

        var resultContents = result[2].Contents.OfType<FunctionResultContent>().ToList();
        Assert.Equal(2, resultContents.Count);

        // First result (large) should be masked
        Assert.Contains("[Masked:", resultContents[0].Result?.ToString());

        // Second result (small) should be unchanged
        Assert.Equal("ok", resultContents[1].Result?.ToString());
    }

    #endregion

    #region System Messages

    [Fact]
    public void MaskObservations_SystemMessagesNeverMasked()
    {
        var masker = new ToolResultMasker(protectedTurns: 1, minimumResultLength: 10);

        var history = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a helpful assistant"),
            new(ChatRole.User, "Old request"),
            CreateAssistantWithToolCall("call-1", "tool"),
            CreateToolResult("call-1", new string('x', 200)),
            new(ChatRole.User, "Recent"),
            new(ChatRole.Assistant, "OK"),
        };

        var result = masker.Mask(history);

        // System message untouched
        Assert.Equal("You are a helpful assistant", result[0].Text);
    }

    #endregion

    #region Constructor Validation

    [Fact]
    public void Constructor_NegativeProtectedTurns_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ToolResultMasker(protectedTurns: -1));
    }

    [Fact]
    public void Constructor_NegativeMinimumResultLength_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ToolResultMasker(minimumResultLength: -1));
    }

    #endregion

    #region Helper Methods

    #region Stored history

    [Fact]
    public void MaskObservations_AnAlreadyMaskedResult_IsLeftAsItIs()
    {
        // A loop stores the prepared history, so the next request masks placeholders it made itself. A placeholder with long
        // arguments is longer than the minimum length; masking it again would describe the placeholder, not the result.
        var masker = new ToolResultMasker(protectedTurns: 1, minimumResultLength: 50);
        var arguments = new Dictionary<string, object?> { ["path"] = "src/" + new string('a', 150) + ".cs" };
        var history = new List<ChatMessage>
        {
            new(ChatRole.User, "Read the file"),
            new(ChatRole.Assistant, [new FunctionCallContent("call-1", "read_file", arguments)]),
            CreateToolResult("call-1", new string('x', 5000)),
            new(ChatRole.User, "Thanks"),
        };

        var once = masker.Mask(history);
        var twice = masker.Mask(once);

        Assert.Same(once, twice);
        Assert.Contains("5,000 chars", once[2].Contents.OfType<FunctionResultContent>().Single().Result?.ToString());
    }

    [Fact]
    public void MaskObservations_WithAClearThreshold_WaitsUntilEnoughIsLeftToMask()
    {
        // Each new turn moves the protection boundary by one turn; masking at every move changes the prompt prefix every
        // turn. With minimumClearTokens the masker waits until the results past the boundary are worth that change.
        var masker = new ToolResultMasker(protectedTurns: 1, minimumResultLength: 50, minimumClearTokens: 2_000,
            tokenCounter: new CharCounter());
        var history = new List<ChatMessage>
        {
            new(ChatRole.User, "Read a"),
            CreateAssistantWithToolCall("call-1", "read_file"),
            CreateToolResult("call-1", new string('a', 1_500)),
            new(ChatRole.User, "Read b"),
        };

        Assert.Same(history, masker.Mask(history));

        history.Add(CreateAssistantWithToolCall("call-2", "read_file"));
        history.Add(CreateToolResult("call-2", new string('b', 1_500)));
        history.Add(new ChatMessage(ChatRole.User, "Read c"));
        var masked = masker.Mask(history);

        Assert.NotSame(history, masked);
        Assert.All(masked.Where(m => m.Role == ChatRole.Tool),
            m => Assert.Contains("[Masked:", m.Contents.OfType<FunctionResultContent>().Single().Result?.ToString()));
    }

    [Fact]
    public void MaskObservations_AClearThreshold_DoesNotCountResultsAlreadyMasked()
    {
        var masker = new ToolResultMasker(protectedTurns: 1, minimumResultLength: 50, minimumClearTokens: 2_000,
            tokenCounter: new CharCounter());
        var history = new List<ChatMessage>
        {
            new(ChatRole.User, "Read a"),
            CreateAssistantWithToolCall("call-1", "read_file"),
            CreateToolResult("call-1", new string('a', 2_500)),
            new(ChatRole.User, "Read b"),
            CreateAssistantWithToolCall("call-2", "read_file"),
            CreateToolResult("call-2", new string('b', 1_000)),
            new(ChatRole.User, "Read c"),
        };

        // The first request masks both (3,500 past the boundary); the stored placeholders do not count towards the next one
        var stored = masker.Mask(history).ToList();
        stored.Add(CreateAssistantWithToolCall("call-3", "read_file"));
        stored.Add(CreateToolResult("call-3", new string('c', 1_000)));
        stored.Add(new ChatMessage(ChatRole.User, "Read d"));

        Assert.Same(stored, masker.Mask(stored));
    }

    [Fact]
    public void MaskObservations_AClearThreshold_LeavesATokenBudgetEnforced()
    {
        // protectedTokens is a limit the caller set: results past it are masked even when they add up to less than the threshold
        var masker = new ToolResultMasker(protectedTurns: 3, minimumResultLength: 50, protectedTokens: 1_200, minimumClearTokens: 10_000,
            tokenCounter: new CharCounter());
        var history = new List<ChatMessage> { new(ChatRole.User, "Read every part") };
        for (var r = 1; r <= 3; r++)
        {
            history.Add(CreateAssistantWithToolCall($"call-{r}", "read_file"));
            history.Add(CreateToolResult($"call-{r}", new string('p', 1_000)));
        }

        var masked = masker.Mask(history);

        Assert.Contains("[Masked:", masked[2].Contents.OfType<FunctionResultContent>().Single().Result?.ToString());
        Assert.DoesNotContain("[Masked:", masked[6].Contents.OfType<FunctionResultContent>().Single().Result?.ToString());
    }

    private sealed class CharCounter : ITokenCounter
    {
        public int Count(string text) => text.Length;

        public int Count(IEnumerable<string> texts) => texts.Sum(t => t.Length);

        public bool SupportsModel(string modelId) => true;
    }

    #endregion

    private static ChatMessage CreateAssistantWithToolCall(string callId, string toolName)
    {
        return new ChatMessage(ChatRole.Assistant,
            [new FunctionCallContent(callId, toolName, new Dictionary<string, object?>())]);
    }

    private static ChatMessage CreateToolResult(string callId, string result)
    {
        return new ChatMessage(ChatRole.Tool,
            [new FunctionResultContent(callId, result)]);
    }

    private static ChatMessage ApplyMaskingToSingleToolResult(
        ToolResultMasker masker,
        string toolName,
        string resultText,
        string callId = "call-1")
    {
        var history = new List<ChatMessage>
        {
            new(ChatRole.User, "Request"),
            CreateAssistantWithToolCall(callId, toolName),
            CreateToolResult(callId, resultText),
            // Protected turn
            new(ChatRole.User, "Next"),
            new(ChatRole.Assistant, "OK"),
        };

        var result = masker.Mask(history);
        return result[2]; // The tool message
    }

    #endregion
}
