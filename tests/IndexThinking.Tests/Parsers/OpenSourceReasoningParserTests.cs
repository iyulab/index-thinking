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
        var result = _parser.StripThinkTags(null!);

        result.Should().BeNull();
    }

    [Fact]
    public void StripThinkTags_NoTags_ReturnsOriginal()
    {
        var input = "Just plain text without any tags";

        var result = _parser.StripThinkTags(input);

        result.Should().Be(input);
    }

    [Fact]
    public void StripThinkTags_WithTags_RemovesTags()
    {
        var input = "<think>Hidden thinking process</think>The visible answer";

        var result = _parser.StripThinkTags(input);

        result.Should().Be("The visible answer");
    }

    [Fact]
    public void StripThinkTags_MultipleThinkBlocks_RemovesAll()
    {
        var input = "<think>First thought</think>Middle<think>Second thought</think>End";

        var result = _parser.StripThinkTags(input);

        result.Should().Be("MiddleEnd");
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
}
