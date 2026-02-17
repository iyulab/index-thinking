using System.Text.Json;
using FluentAssertions;
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

public class AnthropicThinkingConfigTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var config = new AnthropicThinkingConfig();

        config.Type.Should().Be("enabled");
        config.BudgetTokens.Should().Be(10000);
    }

    [Fact]
    public void ShouldDeserialize_FromJson()
    {
        var json = """{"type":"enabled","budget_tokens":5000}""";

        var config = JsonSerializer.Deserialize<AnthropicThinkingConfig>(json);

        config.Should().NotBeNull();
        config!.Type.Should().Be("enabled");
        config.BudgetTokens.Should().Be(5000);
    }

    [Fact]
    public void ShouldRoundtrip_JsonSerialization()
    {
        var original = new AnthropicThinkingConfig { BudgetTokens = 20000 };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<AnthropicThinkingConfig>(json);

        deserialized!.BudgetTokens.Should().Be(20000);
    }
}

public class AnthropicUsageTests
{
    [Fact]
    public void DefaultValues_ShouldBeZero()
    {
        var usage = new AnthropicUsage();

        usage.InputTokens.Should().Be(0);
        usage.OutputTokens.Should().Be(0);
        usage.ThinkingTokens.Should().BeNull();
        usage.CacheReadInputTokens.Should().BeNull();
        usage.CacheCreationInputTokens.Should().BeNull();
    }

    [Fact]
    public void ShouldDeserialize_WithAllFields()
    {
        var json = """
        {
            "input_tokens": 100,
            "output_tokens": 200,
            "thinking_tokens": 50,
            "cache_read_input_tokens": 30,
            "cache_creation_input_tokens": 10
        }
        """;

        var usage = JsonSerializer.Deserialize<AnthropicUsage>(json);

        usage.Should().NotBeNull();
        usage!.InputTokens.Should().Be(100);
        usage.OutputTokens.Should().Be(200);
        usage.ThinkingTokens.Should().Be(50);
        usage.CacheReadInputTokens.Should().Be(30);
        usage.CacheCreationInputTokens.Should().Be(10);
    }

    [Fact]
    public void ShouldDeserialize_WithOptionalFieldsMissing()
    {
        var json = """{"input_tokens": 100, "output_tokens": 200}""";

        var usage = JsonSerializer.Deserialize<AnthropicUsage>(json);

        usage!.ThinkingTokens.Should().BeNull();
        usage.CacheReadInputTokens.Should().BeNull();
    }
}

public class AnthropicMessageResponseTests
{
    [Fact]
    public void DefaultValues_ShouldBeNull()
    {
        var response = new AnthropicMessageResponse();

        response.Content.Should().BeNull();
        response.Model.Should().BeNull();
        response.StopReason.Should().BeNull();
        response.Usage.Should().BeNull();
    }

    [Fact]
    public void ShouldConstruct_WithAllFields()
    {
        var usage = new AnthropicUsage
        {
            InputTokens = 50,
            OutputTokens = 100,
            ThinkingTokens = 75
        };

        var response = new AnthropicMessageResponse
        {
            Model = "claude-sonnet-4-20250514",
            StopReason = "end_turn",
            Usage = usage
        };

        response.Model.Should().Be("claude-sonnet-4-20250514");
        response.StopReason.Should().Be("end_turn");
        response.Usage.Should().NotBeNull();
        response.Usage!.InputTokens.Should().Be(50);
        response.Usage.OutputTokens.Should().Be(100);
        response.Usage.ThinkingTokens.Should().Be(75);
    }

    [Fact]
    public void ContentBlocks_ShouldBeAssignableFromDerivedTypes()
    {
        var content = new AnthropicContentBlock[]
        {
            new AnthropicThinkingBlock { Thinking = "Let me think...", Signature = "sig1" },
            new AnthropicTextBlock { Text = "The answer is 42." }
        };

        var response = new AnthropicMessageResponse
        {
            Content = content,
            Model = "claude-sonnet-4-20250514",
            StopReason = "end_turn"
        };

        response.Content.Should().HaveCount(2);
        response.Content![0].Should().BeOfType<AnthropicThinkingBlock>();
        response.Content[1].Should().BeOfType<AnthropicTextBlock>();
        ((AnthropicThinkingBlock)response.Content[0]).Thinking.Should().Be("Let me think...");
        ((AnthropicTextBlock)response.Content[1]).Text.Should().Be("The answer is 42.");
    }

    [Fact]
    public void ContentBlocks_ShouldIncludeRedactedThinking()
    {
        var content = new AnthropicContentBlock[]
        {
            new AnthropicRedactedThinkingBlock { Data = "encrypted-content" },
            new AnthropicTextBlock { Text = "Response here." }
        };

        var response = new AnthropicMessageResponse { Content = content };

        response.Content.Should().HaveCount(2);
        response.Content![0].Should().BeOfType<AnthropicRedactedThinkingBlock>();
        ((AnthropicRedactedThinkingBlock)response.Content[0]).Data.Should().Be("encrypted-content");
    }
}
