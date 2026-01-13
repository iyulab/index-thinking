using System.Text;
using System.Text.Json;
using FluentAssertions;
using IndexThinking.Core;
using IndexThinking.Parsers;
using IndexThinking.Parsers.Models;
using Microsoft.Extensions.AI;
using Xunit;

namespace IndexThinking.Tests.Parsers;

public class AnthropicReasoningParserTests
{
    private readonly AnthropicReasoningParser _parser = new();

    [Fact]
    public void ProviderFamily_ReturnsAnthropic()
    {
        // Assert
        _parser.ProviderFamily.Should().Be("anthropic");
    }

    [Fact]
    public void TryParse_NullResponse_ReturnsFalse()
    {
        // Act
        var result = _parser.TryParse((ChatResponse)null!, out var content);

        // Assert
        result.Should().BeFalse();
        content.Should().BeNull();
    }

    [Fact]
    public void TryParse_EmptyResponse_ReturnsFalse()
    {
        // Arrange
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, ""));

        // Act
        var result = _parser.TryParse(response, out var content);

        // Assert
        result.Should().BeFalse();
        content.Should().BeNull();
    }

    [Fact]
    public void TryParse_WithThinkingBlock_ReturnsContent()
    {
        // Arrange
        var rawJson = CreateAnthropicResponse(new[]
        {
            ("thinking", "Let me analyze this step by step...", "sig_abc123")
        });
        var response = CreateResponseWithRaw(rawJson);

        // Act
        var result = _parser.TryParse(response, out var content);

        // Assert
        result.Should().BeTrue();
        content.Should().NotBeNull();
        content!.Text.Should().Be("Let me analyze this step by step...");
        content.IsSummarized.Should().BeFalse(); // Anthropic returns full thinking
    }

    [Fact]
    public void TryParse_MultipleThinkingBlocks_CombinesText()
    {
        // Arrange
        var rawJson = CreateAnthropicResponse(new[]
        {
            ("thinking", "First, I'll consider the problem.", "sig_1"),
            ("thinking", "Then, I'll break it down.", "sig_2")
        });
        var response = CreateResponseWithRaw(rawJson);

        // Act
        var result = _parser.TryParse(response, out var content);

        // Assert
        result.Should().BeTrue();
        content!.Text.Should().Contain("First, I'll consider the problem.");
        content.Text.Should().Contain("---"); // Separator
        content.Text.Should().Contain("Then, I'll break it down.");
    }

    [Fact]
    public void TryParse_OnlyTextBlock_ReturnsFalse()
    {
        // Arrange
        var rawJson = """
        {
            "content": [
                { "type": "text", "text": "Here is the answer." }
            ]
        }
        """;
        var response = CreateResponseWithRaw(rawJson);

        // Act
        var result = _parser.TryParse(response, out var content);

        // Assert
        result.Should().BeFalse();
        content.Should().BeNull();
    }

    [Fact]
    public void TryParse_WithThinkingTokens_ExtractsTokenCount()
    {
        // Arrange
        var rawJson = """
        {
            "content": [
                { "type": "thinking", "thinking": "Analyzing...", "signature": "sig123" },
                { "type": "text", "text": "Answer" }
            ],
            "usage": {
                "input_tokens": 100,
                "output_tokens": 200,
                "thinking_tokens": 5000
            }
        }
        """;
        var response = CreateResponseWithRaw(rawJson);

        // Act
        var result = _parser.TryParse(response, out var content);

        // Assert
        result.Should().BeTrue();
        content!.TokenCount.Should().Be(5000);
    }

    [Fact]
    public void ExtractState_NullResponse_ReturnsNull()
    {
        // Act
        var state = _parser.ExtractState((ChatResponse)null!);

        // Assert
        state.Should().BeNull();
    }

    [Fact]
    public void ExtractState_NoThinkingBlocks_ReturnsNull()
    {
        // Arrange
        var rawJson = """
        {
            "content": [
                { "type": "text", "text": "Just text." }
            ]
        }
        """;
        var response = CreateResponseWithRaw(rawJson);

        // Act
        var state = _parser.ExtractState(response);

        // Assert
        state.Should().BeNull();
    }

    [Fact]
    public void ExtractState_WithThinkingBlocks_PreservesSignatures()
    {
        // Arrange
        var rawJson = CreateAnthropicResponse(new[]
        {
            ("thinking", "My reasoning process...", "signature_abc_xyz_123")
        });
        var response = CreateResponseWithRaw(rawJson);

        // Act
        var state = _parser.ExtractState(response);

        // Assert
        state.Should().NotBeNull();
        state!.Provider.Should().Be("anthropic");

        // Verify the state can be restored
        var restored = AnthropicReasoningParser.RestoreState(state);
        restored.Should().NotBeNull();
        restored!.ThinkingBlocks.Should().HaveCount(1);
        restored.ThinkingBlocks[0].Signature.Should().Be("signature_abc_xyz_123");
    }

    [Fact]
    public void ExtractState_WithRedactedThinking_PreservesData()
    {
        // Arrange
        var rawJson = """
        {
            "content": [
                { "type": "thinking", "thinking": "Safe reasoning...", "signature": "sig1" },
                { "type": "redacted_thinking", "data": "encrypted_redacted_content" },
                { "type": "text", "text": "Final answer" }
            ]
        }
        """;
        var response = CreateResponseWithRaw(rawJson);

        // Act
        var state = _parser.ExtractState(response);
        var restored = AnthropicReasoningParser.RestoreState(state!);

        // Assert
        restored.Should().NotBeNull();
        restored!.ThinkingBlocks.Should().HaveCount(1);
        restored.RedactedBlocks.Should().HaveCount(1);
        restored.RedactedBlocks[0].Data.Should().Be("encrypted_redacted_content");
    }

    [Fact]
    public void HasRedactedThinking_WithRedacted_ReturnsTrue()
    {
        // Arrange
        var rawJson = """
        {
            "content": [
                { "type": "redacted_thinking", "data": "encrypted" },
                { "type": "text", "text": "Answer" }
            ]
        }
        """;
        var response = CreateResponseWithRaw(rawJson);

        // Act
        var hasRedacted = _parser.HasRedactedThinking(response);

        // Assert
        hasRedacted.Should().BeTrue();
    }

    [Fact]
    public void HasRedactedThinking_WithoutRedacted_ReturnsFalse()
    {
        // Arrange
        var rawJson = CreateAnthropicResponse(new[]
        {
            ("thinking", "Normal thinking", "sig1")
        });
        var response = CreateResponseWithRaw(rawJson);

        // Act
        var hasRedacted = _parser.HasRedactedThinking(response);

        // Assert
        hasRedacted.Should().BeFalse();
    }

    [Fact]
    public void TryParse_ContentBlocks_DirectParsing_Works()
    {
        // Arrange
        var blocks = new AnthropicContentBlock[]
        {
            new AnthropicThinkingBlock { Thinking = "Direct thinking", Signature = "sig" },
            new AnthropicTextBlock { Text = "Answer" }
        };

        // Act
        var result = _parser.TryParse(blocks, out var content);

        // Assert
        result.Should().BeTrue();
        content!.Text.Should().Be("Direct thinking");
    }

    [Fact]
    public void ExtractState_ContentBlocks_DirectExtraction_Works()
    {
        // Arrange
        var blocks = new AnthropicContentBlock[]
        {
            new AnthropicThinkingBlock { Thinking = "Reasoning", Signature = "direct_sig" }
        };

        // Act
        var state = _parser.ExtractState(blocks);

        // Assert
        state.Should().NotBeNull();
        state!.Provider.Should().Be("anthropic");

        var restored = AnthropicReasoningParser.RestoreState(state);
        restored!.ThinkingBlocks[0].Signature.Should().Be("direct_sig");
    }

    [Fact]
    public void RestoreState_InvalidState_ReturnsNull()
    {
        // Arrange
        var invalidState = new ReasoningState
        {
            Provider = "anthropic",
            Data = Encoding.UTF8.GetBytes("not valid json{{{")
        };

        // Act
        var restored = AnthropicReasoningParser.RestoreState(invalidState);

        // Assert
        restored.Should().BeNull();
    }

    [Fact]
    public void RestoreState_WrongProvider_ReturnsNull()
    {
        // Arrange
        var wrongProvider = new ReasoningState
        {
            Provider = "openai",
            Data = Encoding.UTF8.GetBytes("{}")
        };

        // Act
        var restored = AnthropicReasoningParser.RestoreState(wrongProvider);

        // Assert
        restored.Should().BeNull();
    }

    [Fact]
    public void TryParse_MixedContentTypes_OnlyParsesThinking()
    {
        // Arrange
        var rawJson = """
        {
            "content": [
                { "type": "thinking", "thinking": "Deep analysis...", "signature": "sig" },
                { "type": "text", "text": "The answer is 42." },
                { "type": "thinking", "thinking": "Additional thoughts...", "signature": "sig2" }
            ]
        }
        """;
        var response = CreateResponseWithRaw(rawJson);

        // Act
        var result = _parser.TryParse(response, out var content);

        // Assert
        result.Should().BeTrue();
        content!.Text.Should().Contain("Deep analysis");
        content.Text.Should().Contain("Additional thoughts");
        content.Text.Should().NotContain("The answer is 42");
    }

    private static ChatResponse CreateResponseWithRaw(string json)
    {
        var jsonElement = JsonDocument.Parse(json).RootElement.Clone();
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, ""))
        {
            RawRepresentation = jsonElement
        };
        return response;
    }

    private static string CreateAnthropicResponse((string type, string content, string signature)[] blocks)
    {
        var contentBlocks = blocks.Select(b =>
            $"{{ \"type\": \"{b.type}\", \"thinking\": \"{b.content}\", \"signature\": \"{b.signature}\" }}");

        return $$"""
        {
            "content": [
                {{string.Join(",", contentBlocks)}},
                { "type": "text", "text": "Final answer" }
            ]
        }
        """;
    }
}
