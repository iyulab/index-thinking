using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.AI;
using IndexThinking.Core;
using IndexThinking.Parsers;
using IndexThinking.Parsers.Models;
using Xunit;

namespace IndexThinking.Tests.Parsers;

public class GeminiReasoningParserTests
{
    private readonly GeminiReasoningParser _parser = new();

    [Fact]
    public void ProviderFamily_ReturnsGemini()
    {
        _parser.ProviderFamily.Should().Be("gemini");
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
    public void TryParse_WithThoughtContent_ReturnsContent()
    {
        var geminiResponse = CreateGeminiResponse(
            thought: "Let me analyze this step by step...",
            text: "The answer is 42.",
            thoughtSignature: null
        );
        var response = CreateResponseWithRaw(geminiResponse);

        var result = _parser.TryParse(response, out var content);

        result.Should().BeTrue();
        content.Should().NotBeNull();
        content!.Text.Should().Be("Let me analyze this step by step...");
        content.IsSummarized.Should().BeFalse();
    }

    [Fact]
    public void TryParse_MultipleThoughtParts_CombinesText()
    {
        var geminiResponse = new GeminiResponse
        {
            Candidates = new[]
            {
                new GeminiCandidate
                {
                    Content = new GeminiContent
                    {
                        Role = "model",
                        Parts = new[]
                        {
                            new GeminiContentPart { Thought = "First, let me think about this..." },
                            new GeminiContentPart { Thought = "Now, considering another angle..." },
                            new GeminiContentPart { Text = "The conclusion is..." }
                        }
                    }
                }
            }
        };
        var response = CreateResponseWithRaw(geminiResponse);

        var result = _parser.TryParse(response, out var content);

        result.Should().BeTrue();
        content!.Text.Should().Contain("First, let me think about this...");
        content.Text.Should().Contain("Now, considering another angle...");
        content.Text.Should().Contain("---"); // Separator
    }

    [Fact]
    public void TryParse_NoThoughtContent_ReturnsFalse()
    {
        var geminiResponse = CreateGeminiResponse(
            thought: null,
            text: "Just a plain response",
            thoughtSignature: null
        );
        var response = CreateResponseWithRaw(geminiResponse);

        var result = _parser.TryParse(response, out var content);

        result.Should().BeFalse();
        content.Should().BeNull();
    }

    [Fact]
    public void TryParse_WithThoughtsTokenCount_ExtractsTokenCount()
    {
        var geminiResponse = new GeminiResponse
        {
            Candidates = new[]
            {
                new GeminiCandidate
                {
                    Content = new GeminiContent
                    {
                        Parts = new[]
                        {
                            new GeminiContentPart { Thought = "Some thinking..." }
                        }
                    }
                }
            },
            UsageMetadata = new GeminiUsageMetadata
            {
                ThoughtsTokenCount = 150,
                CandidatesTokenCount = 50
            }
        };
        var response = CreateResponseWithRaw(geminiResponse);

        var result = _parser.TryParse(response, out var content);

        result.Should().BeTrue();
        content!.TokenCount.Should().Be(150);
    }

    [Fact]
    public void TryParse_ContentParts_DirectParsing_Works()
    {
        var parts = new List<GeminiContentPart>
        {
            new() { Thought = "Direct parsing test" },
            new() { Text = "Result" }
        };

        var result = GeminiReasoningParser.TryParse(parts, out var content);

        result.Should().BeTrue();
        content!.Text.Should().Be("Direct parsing test");
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
    public void ExtractState_NoThoughtSignature_ReturnsNull()
    {
        var geminiResponse = CreateGeminiResponse(
            thought: "Some thinking",
            text: "Answer",
            thoughtSignature: null
        );
        var response = CreateResponseWithRaw(geminiResponse);

        var result = _parser.ExtractState(response);

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractState_WithThoughtSignature_ReturnsState()
    {
        var signature = "encrypted_thought_signature_data_abc123";
        var geminiResponse = CreateGeminiResponse(
            thought: "Thinking process",
            text: "Final answer",
            thoughtSignature: signature
        );
        var response = CreateResponseWithRaw(geminiResponse);

        var result = _parser.ExtractState(response);

        result.Should().NotBeNull();
        result!.Provider.Should().Be("gemini");
        result.Data.Should().NotBeEmpty();

        // Verify we can restore the signature
        var restored = GeminiReasoningParser.RestoreThoughtSignature(result);
        restored.Should().Be(signature);
    }

    [Fact]
    public void ExtractState_OpenAICompatibleFormat_ExtractsSignature()
    {
        var signature = "compat_signature_xyz";
        var response = CreateOpenAICompatibleResponse(signature);

        var result = _parser.ExtractState(response);

        result.Should().NotBeNull();
        var restored = GeminiReasoningParser.RestoreThoughtSignature(result!);
        restored.Should().Be(signature);
    }

    [Fact]
    public void ExtractState_ContentParts_DirectExtraction_Works()
    {
        var signature = "direct_signature_test";
        var parts = new List<GeminiContentPart>
        {
            new() { Thought = "Thinking", ThoughtSignature = signature }
        };

        var result = GeminiReasoningParser.ExtractState(parts);

        result.Should().NotBeNull();
        var restored = GeminiReasoningParser.RestoreThoughtSignature(result!);
        restored.Should().Be(signature);
    }

    #endregion

    #region RestoreThoughtSignature Tests

    [Fact]
    public void RestoreThoughtSignature_NullState_ReturnsNull()
    {
        var result = GeminiReasoningParser.RestoreThoughtSignature(null!);

        result.Should().BeNull();
    }

    [Fact]
    public void RestoreThoughtSignature_WrongProvider_ReturnsNull()
    {
        var state = new ReasoningState
        {
            Provider = "openai",
            Data = Encoding.UTF8.GetBytes("some data")
        };

        var result = GeminiReasoningParser.RestoreThoughtSignature(state);

        result.Should().BeNull();
    }

    [Fact]
    public void RestoreThoughtSignature_EmptyData_ReturnsNull()
    {
        var state = new ReasoningState
        {
            Provider = "gemini",
            Data = Array.Empty<byte>()
        };

        var result = GeminiReasoningParser.RestoreThoughtSignature(state);

        result.Should().BeNull();
    }

    [Fact]
    public void RestoreThoughtSignature_ValidState_ReturnsSignature()
    {
        var originalSignature = "test_signature_12345";
        var state = new ReasoningState
        {
            Provider = "gemini",
            Data = Encoding.UTF8.GetBytes(originalSignature)
        };

        var result = GeminiReasoningParser.RestoreThoughtSignature(state);

        result.Should().Be(originalSignature);
    }

    #endregion

    #region HasThoughtSignature Tests

    [Fact]
    public void HasThoughtSignature_NullResponse_ReturnsFalse()
    {
        var result = GeminiReasoningParser.HasThoughtSignature(null!);

        result.Should().BeFalse();
    }

    [Fact]
    public void HasThoughtSignature_NoSignature_ReturnsFalse()
    {
        var geminiResponse = CreateGeminiResponse(
            thought: "Thinking",
            text: "Answer",
            thoughtSignature: null
        );
        var response = CreateResponseWithRaw(geminiResponse);

        var result = GeminiReasoningParser.HasThoughtSignature(response);

        result.Should().BeFalse();
    }

    [Fact]
    public void HasThoughtSignature_WithSignature_ReturnsTrue()
    {
        var geminiResponse = CreateGeminiResponse(
            thought: "Thinking",
            text: "Answer",
            thoughtSignature: "sig_123"
        );
        var response = CreateResponseWithRaw(geminiResponse);

        var result = GeminiReasoningParser.HasThoughtSignature(response);

        result.Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private static GeminiResponse CreateGeminiResponse(string? thought, string text, string? thoughtSignature)
    {
        var parts = new List<GeminiContentPart>();

        if (thought != null)
        {
            parts.Add(new GeminiContentPart
            {
                Thought = thought,
                ThoughtSignature = thoughtSignature
            });
        }

        parts.Add(new GeminiContentPart { Text = text });

        return new GeminiResponse
        {
            Candidates = new[]
            {
                new GeminiCandidate
                {
                    Content = new GeminiContent
                    {
                        Role = "model",
                        Parts = parts
                    },
                    FinishReason = "STOP"
                }
            }
        };
    }

    private static ChatResponse CreateResponseWithRaw(GeminiResponse geminiResponse)
    {
        var json = JsonSerializer.Serialize(geminiResponse);
        using var doc = JsonDocument.Parse(json);
        var rawElement = doc.RootElement.Clone();

        return new ChatResponse([])
        {
            RawRepresentation = rawElement
        };
    }

    private static ChatResponse CreateOpenAICompatibleResponse(string thoughtSignature)
    {
        var response = new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = "Answer",
                        extra_content = new
                        {
                            google = new
                            {
                                thought_signature = thoughtSignature
                            }
                        }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(response);
        using var doc = JsonDocument.Parse(json);
        var rawElement = doc.RootElement.Clone();

        return new ChatResponse([])
        {
            RawRepresentation = rawElement
        };
    }

    #endregion
}
