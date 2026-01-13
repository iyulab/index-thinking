using FluentAssertions;
using IndexThinking.Tokenization;
using Microsoft.Extensions.AI;
using Xunit;

namespace IndexThinking.Tests.Tokenization;

public class TiktokenTokenCounterTests
{
    private readonly TiktokenTokenCounter _counter;

    public TiktokenTokenCounterTests()
    {
        _counter = new TiktokenTokenCounter(ModelEncodingRegistry.O200kBase);
    }

    [Fact]
    public void Count_EmptyString_ReturnsZero()
    {
        // Act
        var count = _counter.Count("");

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public void Count_NullString_ReturnsZero()
    {
        // Act
        var count = _counter.Count((string)null!);

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public void Count_HelloWorld_ReturnsExpectedTokens()
    {
        // Arrange - "Hello World" tokenizes to ["Hello", " World"] in tiktoken
        var text = "Hello World";

        // Act
        var count = _counter.Count(text);

        // Assert
        // Verified with Python tiktoken: tiktoken.get_encoding("o200k_base").encode("Hello World") = [13225, 5765]
        count.Should().Be(2);
    }

    [Fact]
    public void Count_LongerText_ReturnsReasonableCount()
    {
        // Arrange
        var text = "The quick brown fox jumps over the lazy dog.";

        // Act
        var count = _counter.Count(text);

        // Assert
        // Should be around 10-12 tokens for this sentence
        count.Should().BeInRange(8, 15);
    }

    [Fact]
    public void Count_KoreanText_ReturnsTokens()
    {
        // Arrange
        var text = "안녕하세요";

        // Act
        var count = _counter.Count(text);

        // Assert
        // Korean text typically uses more tokens than character count
        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Count_ChatMessage_IncludesOverhead()
    {
        // Arrange
        var message = new ChatMessage(ChatRole.User, "Hello");
        var textOnly = _counter.Count("Hello");

        // Act
        var messageCount = _counter.Count(message);

        // Assert
        messageCount.Should().Be(textOnly + TiktokenTokenCounter.MessageOverhead);
    }

    [Fact]
    public void Count_ChatMessageWithEmptyContent_ReturnsOverheadOnly()
    {
        // Arrange
        var message = new ChatMessage(ChatRole.User, "");

        // Act
        var count = _counter.Count(message);

        // Assert
        count.Should().Be(TiktokenTokenCounter.MessageOverhead);
    }

    [Theory]
    [InlineData("gpt-4o", true)]
    [InlineData("gpt-4o-mini", true)]
    [InlineData("o1", true)]
    [InlineData("o3-mini", true)]
    [InlineData("gpt-4", false)] // Different encoding
    [InlineData("claude-3", false)]
    public void SupportsModel_O200kEncoding_ReturnsExpected(string modelId, bool expected)
    {
        // Act
        var result = _counter.SupportsModel(modelId);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ForModel_ValidModel_ReturnsCounter()
    {
        // Act
        var counter = TiktokenTokenCounter.ForModel("gpt-4o");

        // Assert
        counter.Should().NotBeNull();
        counter.Encoding.Should().Be(ModelEncodingRegistry.O200kBase);
    }

    [Fact]
    public void ForModel_InvalidModel_ThrowsArgumentException()
    {
        // Act
        var action = () => TiktokenTokenCounter.ForModel("claude-3");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Unknown model*");
    }

    [Fact]
    public void Constructor_InvalidEncoding_ThrowsArgumentException()
    {
        // Act
        var action = () => new TiktokenTokenCounter("invalid_encoding");

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Count_CodeWithSpecialChars_CountsCorrectly()
    {
        // Arrange
        var code = "public void Method() { Console.WriteLine(\"Hello\"); }";

        // Act
        var count = _counter.Count(code);

        // Assert
        count.Should().BeGreaterThan(5);
    }

    [Fact]
    public void Count_Cl100kEncoding_WorksCorrectly()
    {
        // Arrange
        var counter = new TiktokenTokenCounter(ModelEncodingRegistry.Cl100kBase);
        var text = "Hello World";

        // Act
        var count = counter.Count(text);

        // Assert
        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Encoding_Property_ReturnsCorrectValue()
    {
        // Assert
        _counter.Encoding.Should().Be(ModelEncodingRegistry.O200kBase);
    }
}
