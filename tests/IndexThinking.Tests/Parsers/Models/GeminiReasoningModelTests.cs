using System.Text.Json;
using AwesomeAssertions;
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
