using System.Text.Json;
using AwesomeAssertions;
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
