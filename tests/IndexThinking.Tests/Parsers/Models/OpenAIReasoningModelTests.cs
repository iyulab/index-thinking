using System.Text.Json;
using FluentAssertions;
using IndexThinking.Parsers.Models;
using Xunit;

namespace IndexThinking.Tests.Parsers.Models;

public class OpenAIReasoningItemTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var item = new OpenAIReasoningItem();

        item.Type.Should().Be("reasoning");
        item.Id.Should().BeNull();
        item.Summary.Should().BeNull();
        item.EncryptedContent.Should().BeNull();
        item.Status.Should().BeNull();
    }

    [Fact]
    public void ShouldDeserialize_FromJson()
    {
        var json = """
        {
            "type": "reasoning",
            "id": "rs_001",
            "summary": [
                {"type": "summary_text", "text": "I analyzed the data."}
            ],
            "status": "completed"
        }
        """;

        var item = JsonSerializer.Deserialize<OpenAIReasoningItem>(json);

        item.Should().NotBeNull();
        item!.Type.Should().Be("reasoning");
        item.Id.Should().Be("rs_001");
        item.Summary.Should().HaveCount(1);
        item.Summary![0].Text.Should().Be("I analyzed the data.");
        item.Status.Should().Be("completed");
    }

    [Fact]
    public void ShouldDeserialize_WithEncryptedContent()
    {
        var json = """
        {
            "type": "reasoning",
            "id": "rs_002",
            "encrypted_content": "base64encodeddata==",
            "status": "completed"
        }
        """;

        var item = JsonSerializer.Deserialize<OpenAIReasoningItem>(json);

        item!.EncryptedContent.Should().Be("base64encodeddata==");
    }

    [Fact]
    public void ShouldRoundtrip_JsonSerialization()
    {
        var original = new OpenAIReasoningItem
        {
            Id = "rs_roundtrip",
            Summary = new[]
            {
                new OpenAIReasoningSummary { Text = "Summary text" }
            },
            Status = "completed"
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<OpenAIReasoningItem>(json);

        deserialized!.Id.Should().Be("rs_roundtrip");
        deserialized.Summary.Should().HaveCount(1);
        deserialized.Status.Should().Be("completed");
    }
}

public class OpenAIReasoningSummaryTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var summary = new OpenAIReasoningSummary();

        summary.Type.Should().Be("summary_text");
        summary.Text.Should().BeNull();
    }

    [Fact]
    public void ShouldDeserialize_FromJson()
    {
        var json = """{"type": "summary_text", "text": "The reasoning conclusion."}""";

        var summary = JsonSerializer.Deserialize<OpenAIReasoningSummary>(json);

        summary!.Type.Should().Be("summary_text");
        summary.Text.Should().Be("The reasoning conclusion.");
    }
}

public class OpenAIReasoningConfigTests
{
    [Fact]
    public void DefaultValues_ShouldBeNull()
    {
        var config = new OpenAIReasoningConfig();

        config.Effort.Should().BeNull();
        config.GenerateSummary.Should().BeNull();
    }

    [Theory]
    [InlineData("low")]
    [InlineData("medium")]
    [InlineData("high")]
    public void ShouldDeserialize_WithEffortLevels(string effort)
    {
        var json = $$$"""{"effort": "{{{effort}}}", "generate_summary": "auto"}""";

        var config = JsonSerializer.Deserialize<OpenAIReasoningConfig>(json);

        config!.Effort.Should().Be(effort);
        config.GenerateSummary.Should().Be("auto");
    }

    [Fact]
    public void ShouldRoundtrip_JsonSerialization()
    {
        var original = new OpenAIReasoningConfig
        {
            Effort = "high",
            GenerateSummary = "auto"
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<OpenAIReasoningConfig>(json);

        deserialized!.Effort.Should().Be("high");
        deserialized.GenerateSummary.Should().Be("auto");
    }
}

public class OpenAIReasoningUsageTests
{
    [Fact]
    public void DefaultValues_ShouldBeZero()
    {
        var usage = new OpenAIReasoningUsage();

        usage.InputTokens.Should().Be(0);
        usage.OutputTokens.Should().Be(0);
        usage.OutputTokensDetails.Should().BeNull();
    }

    [Fact]
    public void ShouldDeserialize_WithDetails()
    {
        var json = """
        {
            "input_tokens": 150,
            "output_tokens": 300,
            "output_tokens_details": {
                "reasoning_tokens": 200
            }
        }
        """;

        var usage = JsonSerializer.Deserialize<OpenAIReasoningUsage>(json);

        usage!.InputTokens.Should().Be(150);
        usage.OutputTokens.Should().Be(300);
        usage.OutputTokensDetails.Should().NotBeNull();
        usage.OutputTokensDetails!.ReasoningTokens.Should().Be(200);
    }

    [Fact]
    public void ShouldDeserialize_WithoutDetails()
    {
        var json = """{"input_tokens": 100, "output_tokens": 50}""";

        var usage = JsonSerializer.Deserialize<OpenAIReasoningUsage>(json);

        usage!.InputTokens.Should().Be(100);
        usage.OutputTokens.Should().Be(50);
        usage.OutputTokensDetails.Should().BeNull();
    }
}

public class OpenAIOutputTokenDetailsTests
{
    [Fact]
    public void DefaultValues_ShouldBeZero()
    {
        var details = new OpenAIOutputTokenDetails();

        details.ReasoningTokens.Should().Be(0);
    }

    [Fact]
    public void ShouldDeserialize_FromJson()
    {
        var json = """{"reasoning_tokens": 500}""";

        var details = JsonSerializer.Deserialize<OpenAIOutputTokenDetails>(json);

        details!.ReasoningTokens.Should().Be(500);
    }
}
