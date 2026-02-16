using System.Text;
using System.Text.Json;
using FluentAssertions;
using IndexThinking.Core;
using IndexThinking.Parsers;
using IndexThinking.Parsers.Models;
using Microsoft.Extensions.AI;
using Xunit;

namespace IndexThinking.Tests.Parsers;

public class OpenAIReasoningParserTests
{
    private readonly OpenAIReasoningParser _parser = new();

    [Fact]
    public void ProviderFamily_ReturnsOpenAI()
    {
        // Assert
        _parser.ProviderFamily.Should().Be("openai");
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
    public void TryParse_WithReasoningSummary_ReturnsContent()
    {
        // Arrange
        var rawJson = CreateOpenAIReasoningResponse(
            summaryText: "The user is asking about factorials.",
            encryptedContent: null);
        var response = CreateResponseWithRaw(rawJson);

        // Act
        var result = _parser.TryParse(response, out var content);

        // Assert
        result.Should().BeTrue();
        content.Should().NotBeNull();
        content!.Text.Should().Be("The user is asking about factorials.");
        content.IsSummarized.Should().BeTrue();
    }

    [Fact]
    public void TryParse_WithEncryptedContentOnly_ReturnsPlaceholder()
    {
        // Arrange
        var rawJson = CreateOpenAIReasoningResponse(
            summaryText: null,
            encryptedContent: "encrypted_base64_content_here");
        var response = CreateResponseWithRaw(rawJson);

        // Act
        var result = _parser.TryParse(response, out var content);

        // Assert
        result.Should().BeTrue();
        content.Should().NotBeNull();
        content!.Text.Should().Be("[Reasoning content encrypted]");
        content.IsSummarized.Should().BeFalse();
    }

    [Fact]
    public void TryParse_WithBothSummaryAndEncrypted_PrefersSummary()
    {
        // Arrange
        var rawJson = CreateOpenAIReasoningResponse(
            summaryText: "Analyzing the problem step by step.",
            encryptedContent: "encrypted_content");
        var response = CreateResponseWithRaw(rawJson);

        // Act
        var result = _parser.TryParse(response, out var content);

        // Assert
        result.Should().BeTrue();
        content!.Text.Should().Be("Analyzing the problem step by step.");
        content.IsSummarized.Should().BeTrue();
    }

    [Fact]
    public void TryParse_WithReasoningTokens_ExtractsTokenCount()
    {
        // Arrange
        var rawJson = CreateOpenAIReasoningResponseWithUsage(
            summaryText: "Thinking about the problem.",
            reasoningTokens: 1500);
        var response = CreateResponseWithRaw(rawJson);

        // Act
        var result = _parser.TryParse(response, out var content);

        // Assert
        result.Should().BeTrue();
        content!.TokenCount.Should().Be(1500);
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
    public void ExtractState_NoEncryptedContent_ReturnsNull()
    {
        // Arrange
        var rawJson = CreateOpenAIReasoningResponse(
            summaryText: "Just a summary",
            encryptedContent: null);
        var response = CreateResponseWithRaw(rawJson);

        // Act
        var state = _parser.ExtractState(response);

        // Assert
        state.Should().BeNull();
    }

    [Fact]
    public void ExtractState_WithEncryptedContent_ReturnsState()
    {
        // Arrange
        var encryptedContent = "encrypted_base64_reasoning_content";
        var rawJson = CreateOpenAIReasoningResponse(
            summaryText: "Summary text",
            encryptedContent: encryptedContent);
        var response = CreateResponseWithRaw(rawJson);

        // Act
        var state = _parser.ExtractState(response);

        // Assert
        state.Should().NotBeNull();
        state!.Provider.Should().Be("openai");
        Encoding.UTF8.GetString(state.Data).Should().Be(encryptedContent);
    }

    [Fact]
    public void TryParse_ReasoningItem_DirectParsing_Works()
    {
        // Arrange
        var item = new OpenAIReasoningItem
        {
            Summary = new List<OpenAIReasoningSummary>
            {
                new() { Text = "First part of reasoning." },
                new() { Text = "Second part." }
            }
        };

        // Act
        var result = OpenAIReasoningParser.TryParse(item, out var content);

        // Assert
        result.Should().BeTrue();
        content!.Text.Should().Contain("First part of reasoning.");
        content.Text.Should().Contain("Second part.");
    }

    [Fact]
    public void ExtractState_ReasoningItem_DirectExtraction_Works()
    {
        // Arrange
        var item = new OpenAIReasoningItem
        {
            EncryptedContent = "direct_encrypted_content"
        };

        // Act
        var state = OpenAIReasoningParser.ExtractState(item);

        // Assert
        state.Should().NotBeNull();
        state!.Provider.Should().Be("openai");
        Encoding.UTF8.GetString(state.Data).Should().Be("direct_encrypted_content");
    }

    [Fact]
    public void TryParse_MultipleSummaryItems_CombinesText()
    {
        // Arrange
        var rawJson = """
        {
            "output": [
                {
                    "type": "reasoning",
                    "id": "rs_123",
                    "summary": [
                        { "type": "summary_text", "text": "Step 1: Understand the problem." },
                        { "type": "summary_text", "text": "Step 2: Break it down." },
                        { "type": "summary_text", "text": "Step 3: Solve each part." }
                    ]
                }
            ]
        }
        """;
        var response = CreateResponseWithRaw(rawJson);

        // Act
        var result = _parser.TryParse(response, out var content);

        // Assert
        result.Should().BeTrue();
        content!.Text.Should().Contain("Step 1");
        content.Text.Should().Contain("Step 2");
        content.Text.Should().Contain("Step 3");
    }

    [Fact]
    public void TryParse_NoReasoningInOutput_ReturnsFalse()
    {
        // Arrange
        var rawJson = """
        {
            "output": [
                {
                    "type": "message",
                    "content": [{ "type": "text", "text": "Hello!" }]
                }
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

    private static ChatResponse CreateResponseWithRaw(string json)
    {
        var jsonElement = JsonDocument.Parse(json).RootElement.Clone();
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, ""))
        {
            RawRepresentation = jsonElement
        };
        return response;
    }

    private static string CreateOpenAIReasoningResponse(string? summaryText, string? encryptedContent)
    {
        var parts = new List<string>();

        if (summaryText is not null)
        {
            parts.Add($"\"summary\": [{{ \"type\": \"summary_text\", \"text\": \"{summaryText}\" }}]");
        }

        if (encryptedContent is not null)
        {
            parts.Add($"\"encrypted_content\": \"{encryptedContent}\"");
        }

        var propsJson = parts.Count > 0 ? ", " + string.Join(", ", parts) : "";

        return $@"{{
            ""output"": [
                {{
                    ""type"": ""reasoning"",
                    ""id"": ""rs_test""{propsJson}
                }}
            ]
        }}";
    }

    private static string CreateOpenAIReasoningResponseWithUsage(string summaryText, int reasoningTokens)
    {
        return $$"""
        {
            "output": [
                {
                    "type": "reasoning",
                    "id": "rs_test",
                    "summary": [{ "type": "summary_text", "text": "{{summaryText}}" }]
                }
            ],
            "usage": {
                "input_tokens": 100,
                "output_tokens": 500,
                "output_tokens_details": {
                    "reasoning_tokens": {{reasoningTokens}}
                }
            }
        }
        """;
    }
}
