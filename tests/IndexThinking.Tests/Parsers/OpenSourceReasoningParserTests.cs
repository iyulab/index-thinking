using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.AI;
using IndexThinking.Core;
using IndexThinking.Parsers;
using IndexThinking.Parsers.Models;
using Xunit;

namespace IndexThinking.Tests.Parsers;

public class OpenSourceReasoningParserTests
{
    private readonly OpenSourceReasoningParser _parser = new();

    [Fact]
    public void ProviderFamily_ReturnsOpenSource()
    {
        _parser.ProviderFamily.Should().Be("opensource");
    }

    #region TryParse Tests

    [Fact]
    public void TryParse_NullResponse_ReturnsFalse()
    {
        var result = _parser.TryParse((ChatResponse)null!, out var content);

        result.Should().BeFalse();
        content.Should().BeNull();
    }

    [Fact]
    public void TryParse_EmptyResponse_ReturnsFalse()
    {
        var response = new ChatResponse([]);

        var result = _parser.TryParse(response, out var content);

        result.Should().BeFalse();
        content.Should().BeNull();
    }

    [Fact]
    public void TryParse_WithReasoningContent_ReturnsContent()
    {
        var deepseekResponse = CreateDeepSeekResponse(
            reasoningContent: "Let me think about this step by step...",
            content: "The answer is 42."
        );
        var response = CreateResponseWithRaw(deepseekResponse);

        var result = _parser.TryParse(response, out var content);

        result.Should().BeTrue();
        content.Should().NotBeNull();
        content!.Text.Should().Be("Let me think about this step by step...");
        content.IsSummarized.Should().BeFalse();
    }

    [Fact]
    public void TryParse_WithVLLMReasoningField_ReturnsContent()
    {
        var vllmResponse = new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        role = "assistant",
                        reasoning = "Analyzing the problem...",
                        content = "Here's my answer"
                    }
                }
            }
        };
        var response = CreateResponseWithRaw(vllmResponse);

        var result = _parser.TryParse(response, out var content);

        result.Should().BeTrue();
        content!.Text.Should().Be("Analyzing the problem...");
    }

    [Fact]
    public void TryParse_WithThinkTags_ExtractsContent()
    {
        var rawResponse = new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        role = "assistant",
                        content = "<think>\nI need to analyze this carefully.\nStep 1: Understand the problem.\n</think>\n\nThe answer is 42."
                    }
                }
            }
        };
        var response = CreateResponseWithRaw(rawResponse);

        var result = _parser.TryParse(response, out var content);

        result.Should().BeTrue();
        content!.Text.Should().Contain("I need to analyze this carefully");
        content.Text.Should().Contain("Step 1: Understand the problem");
    }

    [Fact]
    public void TryParse_NoReasoningContent_ReturnsFalse()
    {
        var response = CreateDeepSeekResponse(
            reasoningContent: null,
            content: "Just a plain response"
        );
        var chatResponse = CreateResponseWithRaw(response);

        var result = _parser.TryParse(chatResponse, out var content);

        result.Should().BeFalse();
        content.Should().BeNull();
    }

    [Fact]
    public void TryParse_WithReasoningTokens_ExtractsTokenCount()
    {
        var response = new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        role = "assistant",
                        reasoning_content = "Thinking process...",
                        content = "Final answer"
                    }
                }
            },
            usage = new
            {
                completion_tokens_details = new
                {
                    reasoning_tokens = 250
                }
            }
        };
        var chatResponse = CreateResponseWithRaw(response);

        var result = _parser.TryParse(chatResponse, out var content);

        result.Should().BeTrue();
        content!.TokenCount.Should().Be(250);
    }

    [Fact]
    public void TryParse_Message_DirectParsing_Works()
    {
        var message = new OpenSourceReasoningMessage
        {
            Role = "assistant",
            ReasoningContent = "Direct parsing test",
            Content = "Result"
        };

        var result = _parser.TryParse(message, out var content);

        result.Should().BeTrue();
        content!.Text.Should().Be("Direct parsing test");
    }

    [Fact]
    public void TryParse_Message_WithReasoningField_Works()
    {
        var message = new OpenSourceReasoningMessage
        {
            Role = "assistant",
            Reasoning = "vLLM style reasoning",
            Content = "Result"
        };

        var result = _parser.TryParse(message, out var content);

        result.Should().BeTrue();
        content!.Text.Should().Be("vLLM style reasoning");
    }

    [Fact]
    public void TryParse_Message_WithThinkTags_ExtractsContent()
    {
        var message = new OpenSourceReasoningMessage
        {
            Role = "assistant",
            Content = "<think>Embedded thinking</think>The answer"
        };

        var result = _parser.TryParse(message, out var content);

        result.Should().BeTrue();
        content!.Text.Should().Be("Embedded thinking");
    }

    #endregion

    #region ExtractState Tests

    [Fact]
    public void ExtractState_NullResponse_ReturnsNull()
    {
        var result = _parser.ExtractState((ChatResponse)null!);

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractState_NoReasoningContent_ReturnsNull()
    {
        var response = CreateDeepSeekResponse(
            reasoningContent: null,
            content: "Plain response"
        );
        var chatResponse = CreateResponseWithRaw(response);

        var result = _parser.ExtractState(chatResponse);

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractState_WithReasoningContent_ReturnsState()
    {
        var reasoningContent = "Step by step analysis...";
        var response = CreateDeepSeekResponse(
            reasoningContent: reasoningContent,
            content: "Final answer"
        );
        var chatResponse = CreateResponseWithRaw(response);

        var result = _parser.ExtractState(chatResponse);

        result.Should().NotBeNull();
        result!.Provider.Should().Be("opensource");
        result.Data.Should().NotBeEmpty();

        // Verify we can restore the content
        var restored = OpenSourceReasoningParser.RestoreReasoningContent(result);
        restored.Should().Be(reasoningContent);
    }

    [Fact]
    public void ExtractState_Message_DirectExtraction_Works()
    {
        var message = new OpenSourceReasoningMessage
        {
            ReasoningContent = "Direct state extraction",
            Content = "Answer"
        };

        var result = _parser.ExtractState(message);

        result.Should().NotBeNull();
        var restored = OpenSourceReasoningParser.RestoreReasoningContent(result!);
        restored.Should().Be("Direct state extraction");
    }

    #endregion

    #region RestoreReasoningContent Tests

    [Fact]
    public void RestoreReasoningContent_NullState_ReturnsNull()
    {
        var result = OpenSourceReasoningParser.RestoreReasoningContent(null!);

        result.Should().BeNull();
    }

    [Fact]
    public void RestoreReasoningContent_WrongProvider_ReturnsNull()
    {
        var state = new ReasoningState
        {
            Provider = "openai",
            Data = Encoding.UTF8.GetBytes("some data")
        };

        var result = OpenSourceReasoningParser.RestoreReasoningContent(state);

        result.Should().BeNull();
    }

    [Fact]
    public void RestoreReasoningContent_EmptyData_ReturnsNull()
    {
        var state = new ReasoningState
        {
            Provider = "opensource",
            Data = Array.Empty<byte>()
        };

        var result = OpenSourceReasoningParser.RestoreReasoningContent(state);

        result.Should().BeNull();
    }

    [Fact]
    public void RestoreReasoningContent_ValidState_ReturnsContent()
    {
        var originalContent = "Complex reasoning process...";
        var state = new ReasoningState
        {
            Provider = "opensource",
            Data = Encoding.UTF8.GetBytes(originalContent)
        };

        var result = OpenSourceReasoningParser.RestoreReasoningContent(state);

        result.Should().Be(originalContent);
    }

    #endregion

    #region StripThinkTags Tests

    [Fact]
    public void StripThinkTags_NullInput_ReturnsInput()
    {
        var result = OpenSourceReasoningParser.StripThinkTags(null!);

        result.Should().BeNull();
    }

    [Fact]
    public void StripThinkTags_NoTags_ReturnsOriginal()
    {
        var input = "Just plain text without any tags";

        var result = OpenSourceReasoningParser.StripThinkTags(input);

        result.Should().Be(input);
    }

    [Fact]
    public void StripThinkTags_WithTags_RemovesTags()
    {
        var input = "<think>Hidden thinking process</think>The visible answer";

        var result = OpenSourceReasoningParser.StripThinkTags(input);

        result.Should().Be("The visible answer");
    }

    [Fact]
    public void StripThinkTags_MultipleThinkBlocks_RemovesAll()
    {
        var input = "<think>First thought</think>Middle<think>Second thought</think>End";

        var result = OpenSourceReasoningParser.StripThinkTags(input);

        result.Should().Be("MiddleEnd");
    }

    #endregion

    #region StripLeadingUntaggedReasoning Tests

    [Fact]
    public void StripLeadingUntaggedReasoning_NullInput_ReturnsInput()
    {
        var result = OpenSourceReasoningParser.StripLeadingUntaggedReasoning(null!);
        result.Should().BeNull();
    }

    [Fact]
    public void StripLeadingUntaggedReasoning_NoReasoningPattern_ReturnsOriginal()
    {
        var input = "### 1. Topic\nContent here\n\n### 2. Details\nMore content";

        var result = OpenSourceReasoningParser.StripLeadingUntaggedReasoning(input);

        result.Should().Be(input);
    }

    [Fact]
    public void StripLeadingUntaggedReasoning_SingleReasoningParagraph_ReturnsOriginal()
    {
        // Only 1 reasoning paragraph is not enough signal to strip
        var input = "Okay, I need to think about this.\n\n### 1. Topic\nContent here";

        var result = OpenSourceReasoningParser.StripLeadingUntaggedReasoning(input);

        result.Should().Be(input);
    }

    [Fact]
    public void StripLeadingUntaggedReasoning_MultipleReasoningParagraphs_StripsReasoning()
    {
        var input =
            "Okay, I need to continue the response from where it was cut off. " +
            "Let me check the previous part.\n\n" +
            "The user provided a long sermon text, and the assistant had started " +
            "summarizing it. The last part was cut off mid-sentence.\n\n" +
            "So the next part should continue explaining the core arguments. " +
            "Let me look at the original text to see what comes next.\n\n" +
            "### 3. 핵심 논지 전개\n3. **신령인의 형성과 역할**:\ncontent here";

        var result = OpenSourceReasoningParser.StripLeadingUntaggedReasoning(input);

        result.Should().StartWith("### 3.");
        result.Should().Contain("신령인의 형성과 역할");
        result.Should().NotContain("Okay, I need to continue");
    }

    [Fact]
    public void StripLeadingUntaggedReasoning_ReasoningWithDraftContent_StripsEntireBlock()
    {
        // Reasoning block contains draft content (numbered items) between reasoning paragraphs
        var input =
            "Okay, I need to continue the response.\n\n" +
            "Looking at the text, the speaker discusses several points.\n\n" +
            "2. **내적 믿음**: 설명...\n" +
            "3. **우상의 위험**: 설명...\n\n" +
            "Wait, but the user's instruction says to continue directly.\n\n" +
            "Let me check the original text again.\n\n" +
            "### 3. 핵심 논지 전개\n3. **실제 내용**:\nactual content";

        var result = OpenSourceReasoningParser.StripLeadingUntaggedReasoning(input);

        result.Should().StartWith("### 3.");
        result.Should().Contain("실제 내용");
        result.Should().NotContain("Okay, I need to continue");
    }

    [Fact]
    public void StripLeadingUntaggedReasoning_AllReasoning_ReturnsOriginal()
    {
        // If everything is reasoning with no actual content after, don't strip
        var input =
            "Okay, I need to think about this.\n\n" +
            "The user asked about something complex.\n\n" +
            "Let me analyze this further.";

        var result = OpenSourceReasoningParser.StripLeadingUntaggedReasoning(input);

        result.Should().Be(input);
    }

    [Fact]
    public void StripLeadingUntaggedReasoning_ContentFollowedByReasoning_ReturnsOriginal()
    {
        // Content starts normally — this is the trailing case, not leading
        var input = "### 1. Topic\nContent here\n\nOkay, let me think.\n\nThe user wants more.";

        var result = OpenSourceReasoningParser.StripLeadingUntaggedReasoning(input);

        result.Should().Be(input);
    }

    #endregion

    #region StripUntaggedReasoning Tests

    [Fact]
    public void StripUntaggedReasoning_TrailingReasoning_StripsWhenAfterOneThird()
    {
        // Valid content takes up >1/3, reasoning is trailing
        var content = new string('가', 500); // ~500 chars of Korean content
        var reasoning = "\n\nOkay, I need to think about this. " + new string('x', 300);
        var input = content + reasoning;

        var result = OpenSourceReasoningParser.StripUntaggedReasoning(input);

        result.Should().Be(content);
    }

    [Fact]
    public void StripUntaggedReasoning_EarlyReasoning_DoesNotStrip()
    {
        // Reasoning appears before 200 chars — not stripped (handled by StripLeadingUntaggedReasoning)
        var content = "Short content here.";
        var reasoning = "\n\nOkay, I need to think about this. " + new string('x', 1000);
        var input = content + reasoning;

        var result = OpenSourceReasoningParser.StripUntaggedReasoning(input);

        result.Should().Be(input);
    }

    [Fact]
    public void StripUntaggedReasoning_MassiveReasoningDwarfsContent_StillStrips()
    {
        // Bug reproduction: when reasoning block is much larger than content,
        // proportional threshold (text.Length / 3) fails because content < total/3.
        // Fixed: use absolute minimum (200 chars) instead of proportional threshold.
        var content = new string('가', 300); // 300 chars — well above 200 minimum
        var reasoning = "\n\nOkay, I need to continue the response. " +
            new string('x', 200) + "\n\n" +
            "Wait, let me check. " + new string('y', 200) + "\n\n" +
            "The user wants more. " + new string('z', 5000); // massive reasoning block
        var input = content + reasoning;

        var result = OpenSourceReasoningParser.StripUntaggedReasoning(input);

        result.Should().Be(content);
    }

    [Fact]
    public void StripUntaggedReasoning_AlternativelyStarter_Strips()
    {
        var content = new string('가', 300);
        var reasoning = "\n\nAlternatively, maybe the user wants something different. " +
            new string('x', 300);
        var input = content + reasoning;

        var result = OpenSourceReasoningParser.StripUntaggedReasoning(input);

        result.Should().Be(content);
    }

    [Fact]
    public void StripUntaggedReasoning_SoWithComma_Strips()
    {
        var content = new string('가', 300);
        var reasoning = "\n\nSo, the continuation would be to modify section 6. " +
            new string('x', 300);
        var input = content + reasoning;

        var result = OpenSourceReasoningParser.StripUntaggedReasoning(input);

        result.Should().Be(content);
    }

    #endregion

    #region HasReasoningContent Tests

    [Fact]
    public void HasReasoningContent_NullResponse_ReturnsFalse()
    {
        var result = _parser.HasReasoningContent(null!);

        result.Should().BeFalse();
    }

    [Fact]
    public void HasReasoningContent_NoReasoning_ReturnsFalse()
    {
        var response = CreateDeepSeekResponse(
            reasoningContent: null,
            content: "Plain response"
        );
        var chatResponse = CreateResponseWithRaw(response);

        var result = _parser.HasReasoningContent(chatResponse);

        result.Should().BeFalse();
    }

    [Fact]
    public void HasReasoningContent_WithStructuredReasoning_ReturnsTrue()
    {
        var response = CreateDeepSeekResponse(
            reasoningContent: "Thinking...",
            content: "Answer"
        );
        var chatResponse = CreateResponseWithRaw(response);

        var result = _parser.HasReasoningContent(chatResponse);

        result.Should().BeTrue();
    }

    [Fact]
    public void HasReasoningContent_WithThinkTags_ReturnsTrue()
    {
        var response = new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = "<think>Some thinking</think>Answer"
                    }
                }
            }
        };
        var chatResponse = CreateResponseWithRaw(response);

        var result = _parser.HasReasoningContent(chatResponse);

        result.Should().BeTrue();
    }

    #endregion

    #region Custom Think Tags Configuration Tests

    [Fact]
    public void TryParse_CustomThinkTags_ExtractsContent()
    {
        var config = new DeepSeekThinkingConfig
        {
            StartToken = "[[THINK]]",
            EndToken = "[[/THINK]]"
        };
        var customParser = new OpenSourceReasoningParser(config);

        var response = new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = "[[THINK]]Custom thinking[[/THINK]]Answer"
                    }
                }
            }
        };
        var chatResponse = CreateResponseWithRaw(response);

        var result = customParser.TryParse(chatResponse, out var content);

        result.Should().BeTrue();
        content!.Text.Should().Be("Custom thinking");
    }

    #endregion

    #region Helper Methods

    private static object CreateDeepSeekResponse(string? reasoningContent, string content)
    {
        return new
        {
            id = "test-id",
            @object = "chat.completion",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new
                    {
                        role = "assistant",
                        reasoning_content = reasoningContent,
                        content = content
                    },
                    finish_reason = "stop"
                }
            }
        };
    }

    private static ChatResponse CreateResponseWithRaw(object rawResponse)
    {
        var json = JsonSerializer.Serialize(rawResponse);
        using var doc = JsonDocument.Parse(json);
        var rawElement = doc.RootElement.Clone();

        return new ChatResponse([])
        {
            RawRepresentation = rawElement
        };
    }

    #endregion

    #region StripTrailingByScriptShift Tests

    [Fact]
    public void StripTrailingByScriptShift_KoreanContentWithEnglishReasoning_StripsReasoning()
    {
        // Build CJK-dominant text (~800 chars) followed by English reasoning (~300 chars)
        var korean = new StringBuilder();
        korean.AppendLine("### 1. 핵심 주제");
        korean.AppendLine("이 설교는 하나님의 사랑과 은혜에 대한 메시지입니다.");
        korean.AppendLine();
        korean.AppendLine("### 2. 주요 성경 구절");
        korean.AppendLine("요한복음 3장 16절에서 하나님이 세상을 이처럼 사랑하사 독생자를 주셨으니");
        korean.AppendLine();
        korean.AppendLine("### 3. 핵심 메시지");
        korean.AppendLine("하나님의 사랑은 조건 없는 것이며 우리 모두에게 주어진 선물입니다.");
        korean.AppendLine("이 사랑은 어떤 상황에서도 변하지 않는 영원한 사랑입니다.");
        korean.AppendLine();
        korean.AppendLine("### 4. 적용 및 실천");
        korean.AppendLine("우리는 이 사랑을 받아들이고 다른 사람들에게도 전해야 합니다.");
        korean.AppendLine("매일의 삶에서 하나님의 사랑을 실천하는 것이 중요합니다.");
        korean.AppendLine();
        korean.AppendLine("### 5. 결론 및 축복");
        korean.AppendLine("하나님의 사랑 안에서 평안과 기쁨을 누리시기를 축복합니다.");
        korean.AppendLine("이 메시지가 여러분의 삶에 큰 힘이 되기를 바랍니다.");

        var reasoning = new StringBuilder();
        reasoning.AppendLine();
        reasoning.AppendLine("Okay, so the user wanted me to summarize this sermon into 5 sections. " +
            "I've covered the main themes from the transcript. Let me verify that each section " +
            "accurately reflects the content. The first section introduces the core topic of God's love. " +
            "The second section references John 3:16 which was the primary verse discussed. " +
            "I think this covers everything the user requested in their prompt.");

        var text = korean.ToString() + reasoning.ToString();
        var result = OpenSourceReasoningParser.StripTrailingByScriptShift(text);

        result.Should().NotContain("Okay, so the user");
        result.Should().Contain("### 5. 결론 및 축복");
        result.Should().Contain("큰 힘이 되기를 바랍니다");
    }

    [Fact]
    public void StripTrailingByScriptShift_PureKoreanText_NoChange()
    {
        var korean = new StringBuilder();
        for (var i = 0; i < 20; i++)
            korean.AppendLine("하나님의 사랑은 영원하며 우리 모두에게 주어진 선물입니다. 이 사랑을 전하세요.");

        var text = korean.ToString();
        var result = OpenSourceReasoningParser.StripTrailingByScriptShift(text);

        result.Should().Be(text);
    }

    [Fact]
    public void StripTrailingByScriptShift_PureEnglishText_NoChange()
    {
        var english = new StringBuilder();
        for (var i = 0; i < 20; i++)
            english.AppendLine("This is a completely English text with no CJK characters at all.");

        var text = english.ToString();
        var result = OpenSourceReasoningParser.StripTrailingByScriptShift(text);

        result.Should().Be(text);
    }

    [Fact]
    public void StripTrailingByScriptShift_ShortText_NoChange()
    {
        var text = "짧은 한국어 텍스트\n\nShort trailing English.";
        var result = OpenSourceReasoningParser.StripTrailingByScriptShift(text);

        result.Should().Be(text);
    }

    [Fact]
    public void StripTrailingByScriptShift_EnglishTermsInKorean_NoFalsePositive()
    {
        // Korean text with English technical terms mixed in should NOT be stripped
        var korean = new StringBuilder();
        korean.AppendLine("### 1. 핵심 주제");
        korean.AppendLine("이 설교는 하나님의 love와 grace에 대한 메시지입니다.");
        korean.AppendLine();
        korean.AppendLine("### 2. 성경 구절");
        korean.AppendLine("John 3:16 요한복음에서 하나님이 세상을 사랑하사 독생자를 주셨으니");
        korean.AppendLine();
        korean.AppendLine("### 3. 핵심 메시지와 Application");
        korean.AppendLine("하나님의 사랑은 unconditional한 것이며 우리 모두에게 주어진 gift입니다.");
        korean.AppendLine("매일의 삶에서 practice하는 것이 중요합니다.");
        korean.AppendLine();
        korean.AppendLine("### 4. 결론");
        korean.AppendLine("하나님의 사랑 안에서 peace와 joy를 누리시기를 축복합니다.");
        // Pad to meet minimum length
        for (var i = 0; i < 5; i++)
            korean.AppendLine("이 메시지가 여러분의 삶에 큰 힘이 되기를 바라며 축복합니다.");

        var text = korean.ToString();
        var result = OpenSourceReasoningParser.StripTrailingByScriptShift(text);

        result.Should().Be(text);
    }

    #endregion
}
