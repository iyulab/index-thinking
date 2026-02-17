using System.Text.Json;
using FluentAssertions;
using IndexThinking.Parsers.Models;
using Xunit;

namespace IndexThinking.Tests.Parsers.Models;

public class GeminiContentPartTests
{
    [Fact]
    public void DefaultValues_ShouldBeNull()
    {
        var part = new GeminiContentPart();

        part.Text.Should().BeNull();
        part.Thought.Should().BeNull();
        part.ThoughtSignature.Should().BeNull();
        part.FunctionCall.Should().BeNull();
        part.ExtraContent.Should().BeNull();
    }

    [Fact]
    public void ShouldDeserialize_TextPart()
    {
        var json = """{"text": "Hello world"}""";

        var part = JsonSerializer.Deserialize<GeminiContentPart>(json);

        part!.Text.Should().Be("Hello world");
    }

    [Fact]
    public void ShouldDeserialize_ThoughtPart()
    {
        var json = """{"thought": "Let me reason about this...", "thoughtSignature": "sig123"}""";

        var part = JsonSerializer.Deserialize<GeminiContentPart>(json);

        part!.Thought.Should().Be("Let me reason about this...");
        part.ThoughtSignature.Should().Be("sig123");
    }

    [Fact]
    public void ShouldDeserialize_FunctionCallPart()
    {
        var json = """{"functionCall": {"name": "search", "args": {"query": "test"}}}""";

        var part = JsonSerializer.Deserialize<GeminiContentPart>(json);

        part!.FunctionCall.Should().NotBeNull();
        part.FunctionCall!.Name.Should().Be("search");
    }

    [Fact]
    public void ShouldDeserialize_ExtraContentPart()
    {
        var json = """{"extra_content": {"google": {"thought_signature": "compat-sig"}}}""";

        var part = JsonSerializer.Deserialize<GeminiContentPart>(json);

        part!.ExtraContent.Should().NotBeNull();
        part.ExtraContent!.Google.Should().NotBeNull();
        part.ExtraContent.Google!.ThoughtSignature.Should().Be("compat-sig");
    }
}

public class GeminiFunctionCallTests
{
    [Fact]
    public void DefaultValues_ShouldBeNull()
    {
        var call = new GeminiFunctionCall();

        call.Name.Should().BeNull();
        call.Args.Should().BeNull();
    }

    [Fact]
    public void ShouldDeserialize_FromJson()
    {
        var json = """{"name": "get_weather", "args": {"city": "Seoul"}}""";

        var call = JsonSerializer.Deserialize<GeminiFunctionCall>(json);

        call!.Name.Should().Be("get_weather");
        call.Args.Should().NotBeNull();
    }
}

public class GeminiCandidateTests
{
    [Fact]
    public void DefaultValues_ShouldBeNull()
    {
        var candidate = new GeminiCandidate();

        candidate.Content.Should().BeNull();
        candidate.FinishReason.Should().BeNull();
    }

    [Fact]
    public void ShouldDeserialize_WithContent()
    {
        var json = """
        {
            "content": {
                "role": "model",
                "parts": [{"text": "Hello"}]
            },
            "finishReason": "STOP"
        }
        """;

        var candidate = JsonSerializer.Deserialize<GeminiCandidate>(json);

        candidate!.Content.Should().NotBeNull();
        candidate.Content!.Role.Should().Be("model");
        candidate.Content.Parts.Should().HaveCount(1);
        candidate.FinishReason.Should().Be("STOP");
    }
}

public class GeminiResponseTests
{
    [Fact]
    public void DefaultValues_ShouldBeNull()
    {
        var response = new GeminiResponse();

        response.Candidates.Should().BeNull();
        response.UsageMetadata.Should().BeNull();
    }

    [Fact]
    public void ShouldDeserialize_CompleteResponse()
    {
        var json = """
        {
            "candidates": [
                {
                    "content": {
                        "role": "model",
                        "parts": [
                            {"thought": "Reasoning here..."},
                            {"text": "The answer is 42."}
                        ]
                    },
                    "finishReason": "STOP"
                }
            ],
            "usageMetadata": {
                "promptTokenCount": 50,
                "candidatesTokenCount": 100,
                "totalTokenCount": 150,
                "thoughtsTokenCount": 75
            }
        }
        """;

        var response = JsonSerializer.Deserialize<GeminiResponse>(json);

        response.Should().NotBeNull();
        response!.Candidates.Should().HaveCount(1);
        response.Candidates![0].Content!.Parts.Should().HaveCount(2);
        response.Candidates[0].Content!.Parts![0].Thought.Should().Be("Reasoning here...");
        response.Candidates[0].Content!.Parts![1].Text.Should().Be("The answer is 42.");
        response.UsageMetadata!.PromptTokenCount.Should().Be(50);
        response.UsageMetadata.ThoughtsTokenCount.Should().Be(75);
    }
}

public class GeminiUsageMetadataTests
{
    [Fact]
    public void DefaultValues_ShouldBeZero()
    {
        var metadata = new GeminiUsageMetadata();

        metadata.PromptTokenCount.Should().Be(0);
        metadata.CandidatesTokenCount.Should().Be(0);
        metadata.TotalTokenCount.Should().Be(0);
        metadata.ThoughtsTokenCount.Should().Be(0);
    }

    [Fact]
    public void ShouldDeserialize_FromJson()
    {
        var json = """
        {
            "promptTokenCount": 200,
            "candidatesTokenCount": 300,
            "totalTokenCount": 500,
            "thoughtsTokenCount": 150
        }
        """;

        var metadata = JsonSerializer.Deserialize<GeminiUsageMetadata>(json);

        metadata!.PromptTokenCount.Should().Be(200);
        metadata.CandidatesTokenCount.Should().Be(300);
        metadata.TotalTokenCount.Should().Be(500);
        metadata.ThoughtsTokenCount.Should().Be(150);
    }
}

public class GeminiThinkingConfigTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var config = new GeminiThinkingConfig();

        config.ThinkingLevel.Should().BeNull();
        config.ThinkingBudget.Should().BeNull();
        config.IncludeThoughts.Should().BeTrue();
    }

    [Fact]
    public void ShouldDeserialize_Gemini3Config()
    {
        var json = """{"thinkingLevel": "high", "includeThoughts": true}""";

        var config = JsonSerializer.Deserialize<GeminiThinkingConfig>(json);

        config!.ThinkingLevel.Should().Be("high");
        config.IncludeThoughts.Should().BeTrue();
    }

    [Fact]
    public void ShouldDeserialize_LegacyGemini25Config()
    {
        var json = """{"thinkingBudget": 8192, "includeThoughts": true}""";

        var config = JsonSerializer.Deserialize<GeminiThinkingConfig>(json);

        config!.ThinkingBudget.Should().Be(8192);
    }
}
