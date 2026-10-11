using System.Text.Json;
using AwesomeAssertions;
using IndexThinking.Parsers.Models;
using Xunit;

namespace IndexThinking.Tests.Parsers.Models;

public class AnthropicThinkingBlockTests
{
    [Fact]
    public void Type_ShouldBeThinking()
    {
        var block = new AnthropicThinkingBlock
        {
            Thinking = "reasoning",
            Signature = "sig123"
        };

        block.Type.Should().Be("thinking");
    }

    [Fact]
    public void ShouldDeserialize_FromJson()
    {
        var json = """{"type":"thinking","thinking":"Let me analyze this.","signature":"abc123"}""";

        var block = JsonSerializer.Deserialize<AnthropicThinkingBlock>(json);

        block.Should().NotBeNull();
        block!.Thinking.Should().Be("Let me analyze this.");
        block.Signature.Should().Be("abc123");
        block.Type.Should().Be("thinking");
    }

    [Fact]
    public void ShouldSerialize_ToJson()
    {
        var block = new AnthropicThinkingBlock
        {
            Thinking = "My reasoning",
            Signature = "sig456"
        };

        var json = JsonSerializer.Serialize(block);

        json.Should().Contain("\"thinking\":\"My reasoning\"");
        json.Should().Contain("\"signature\":\"sig456\"");
    }

    [Fact]
    public void ShouldRoundtrip_JsonSerialization()
    {
        var original = new AnthropicThinkingBlock
        {
            Thinking = "Step 1: Analyze. Step 2: Conclude.",
            Signature = "roundtrip-sig"
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<AnthropicThinkingBlock>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Thinking.Should().Be(original.Thinking);
        deserialized.Signature.Should().Be(original.Signature);
    }
}

public class AnthropicRedactedThinkingBlockTests
{
    [Fact]
    public void Type_ShouldBeRedactedThinking()
    {
        var block = new AnthropicRedactedThinkingBlock { Data = "encrypted" };

        block.Type.Should().Be("redacted_thinking");
    }

    [Fact]
    public void ShouldDeserialize_FromJson()
    {
        var json = """{"type":"redacted_thinking","data":"encrypted-data-here"}""";

        var block = JsonSerializer.Deserialize<AnthropicRedactedThinkingBlock>(json);

        block.Should().NotBeNull();
        block!.Data.Should().Be("encrypted-data-here");
        block.Type.Should().Be("redacted_thinking");
    }
}

public class AnthropicTextBlockTests
{
    [Fact]
    public void Type_ShouldBeText()
    {
        var block = new AnthropicTextBlock { Text = "Hello" };

        block.Type.Should().Be("text");
    }

    [Fact]
    public void ShouldDeserialize_FromJson()
    {
        var json = """{"type":"text","text":"The answer is 42."}""";

        var block = JsonSerializer.Deserialize<AnthropicTextBlock>(json);

        block.Should().NotBeNull();
        block!.Text.Should().Be("The answer is 42.");
        block.Type.Should().Be("text");
    }
}

public class AnthropicContentBlockPolymorphicTests
{
    [Fact]
    public void Serialize_ThinkingBlock_AsBaseType_ShouldIncludeDiscriminator()
    {
        AnthropicContentBlock block = new AnthropicThinkingBlock
        {
            Thinking = "reasoning text",
            Signature = "sig-abc"
        };

        var json = JsonSerializer.Serialize(block);

        json.Should().Contain("\"type\":\"thinking\"");
        json.Should().Contain("\"thinking\":\"reasoning text\"");
        json.Should().Contain("\"signature\":\"sig-abc\"");
    }

    [Fact]
    public void Serialize_TextBlock_AsBaseType_ShouldIncludeDiscriminator()
    {
        AnthropicContentBlock block = new AnthropicTextBlock { Text = "hello world" };

        var json = JsonSerializer.Serialize(block);

        json.Should().Contain("\"type\":\"text\"");
        json.Should().Contain("\"text\":\"hello world\"");
    }

    [Fact]
    public void Serialize_RedactedThinkingBlock_AsBaseType_ShouldIncludeDiscriminator()
    {
        AnthropicContentBlock block = new AnthropicRedactedThinkingBlock { Data = "enc-data" };

        var json = JsonSerializer.Serialize(block);

        json.Should().Contain("\"type\":\"redacted_thinking\"");
        json.Should().Contain("\"data\":\"enc-data\"");
    }

    [Fact]
    public void Deserialize_ThinkingBlock_AsBaseType_ShouldResolveCorrectType()
    {
        var json = """{"type":"thinking","thinking":"deep thought","signature":"sig-123"}""";

        var block = JsonSerializer.Deserialize<AnthropicContentBlock>(json);

        block.Should().BeOfType<AnthropicThinkingBlock>();
        var thinking = (AnthropicThinkingBlock)block!;
        thinking.Thinking.Should().Be("deep thought");
        thinking.Signature.Should().Be("sig-123");
        thinking.Type.Should().Be("thinking");
    }

    [Fact]
    public void Deserialize_TextBlock_AsBaseType_ShouldResolveCorrectType()
    {
        var json = """{"type":"text","text":"The answer is 42."}""";

        var block = JsonSerializer.Deserialize<AnthropicContentBlock>(json);

        block.Should().BeOfType<AnthropicTextBlock>();
        var text = (AnthropicTextBlock)block!;
        text.Text.Should().Be("The answer is 42.");
        text.Type.Should().Be("text");
    }

    [Fact]
    public void Deserialize_RedactedThinkingBlock_AsBaseType_ShouldResolveCorrectType()
    {
        var json = """{"type":"redacted_thinking","data":"encrypted-payload"}""";

        var block = JsonSerializer.Deserialize<AnthropicContentBlock>(json);

        block.Should().BeOfType<AnthropicRedactedThinkingBlock>();
        var redacted = (AnthropicRedactedThinkingBlock)block!;
        redacted.Data.Should().Be("encrypted-payload");
        redacted.Type.Should().Be("redacted_thinking");
    }

    [Fact]
    public void Roundtrip_PolymorphicSerialization_ShouldPreserveTypes()
    {
        AnthropicContentBlock[] original =
        [
            new AnthropicThinkingBlock { Thinking = "step 1", Signature = "sig-1" },
            new AnthropicRedactedThinkingBlock { Data = "redacted-data" },
            new AnthropicTextBlock { Text = "final answer" }
        ];

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<AnthropicContentBlock[]>(json);

        deserialized.Should().NotBeNull();
        deserialized.Should().HaveCount(3);
        deserialized![0].Should().BeOfType<AnthropicThinkingBlock>();
        deserialized[1].Should().BeOfType<AnthropicRedactedThinkingBlock>();
        deserialized[2].Should().BeOfType<AnthropicTextBlock>();

        var thinking = (AnthropicThinkingBlock)deserialized[0];
        thinking.Thinking.Should().Be("step 1");
        thinking.Signature.Should().Be("sig-1");

        var redacted = (AnthropicRedactedThinkingBlock)deserialized[1];
        redacted.Data.Should().Be("redacted-data");

        var text = (AnthropicTextBlock)deserialized[2];
        text.Text.Should().Be("final answer");
    }
}
