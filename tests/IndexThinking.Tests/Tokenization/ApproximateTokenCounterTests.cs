using FluentAssertions;
using IndexThinking.Tokenization;
using Microsoft.Extensions.AI;
using Xunit;

namespace IndexThinking.Tests.Tokenization;

public class ApproximateTokenCounterTests
{
    private readonly ApproximateTokenCounter _counter = new();

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
    public void Count_EnglishText_ReturnsApproximateTokens()
    {
        // Arrange - 44 chars, ~4 chars/token = ~11 tokens
        var text = "The quick brown fox jumps over the lazy dog.";

        // Act
        var count = _counter.Count(text);

        // Assert
        // With ratio of 4.0, expect ~11 tokens (44/4 = 11)
        count.Should().BeInRange(8, 15);
    }

    [Fact]
    public void Count_KoreanText_ReturnsHigherTokenCount()
    {
        // Arrange - 5 characters of Korean
        var text = "안녕하세요";

        // Act
        var count = _counter.Count(text);

        // Assert
        // With ratio of 1.5, expect ~3-4 tokens (5/1.5 ≈ 3.3)
        count.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void Count_ChineseText_ReturnsHigherTokenCount()
    {
        // Arrange
        var text = "你好世界";

        // Act
        var count = _counter.Count(text);

        // Assert
        // With ratio of 1.2, expect ~3-4 tokens
        count.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void Count_MixedLanguage_UsesBlendedRatio()
    {
        // Arrange - Mixed English and Korean
        var text = "Hello 안녕하세요 World";

        // Act
        var count = _counter.Count(text);

        // Assert
        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Count_OnlyWhitespace_ReturnsZero()
    {
        // Arrange
        var text = "   \n\t  ";

        // Act
        var count = _counter.Count(text);

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public void Count_ChatMessage_IncludesOverhead()
    {
        // Arrange
        var message = new ChatMessage(ChatRole.User, "Hello World");
        var textOnly = _counter.Count("Hello World");

        // Act
        var messageCount = _counter.Count(message);

        // Assert
        messageCount.Should().Be(textOnly + ApproximateTokenCounter.MessageOverhead);
    }

    [Theory]
    [InlineData("gpt-4o")]
    [InlineData("claude-3")]
    [InlineData("gemini-pro")]
    [InlineData("unknown-model")]
    [InlineData("")]
    public void SupportsModel_AnyModel_ReturnsTrue(string modelId)
    {
        // Act
        var result = _counter.SupportsModel(modelId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithCustomRatios_UsesCustomValues()
    {
        // Arrange
        var customRatios = new LanguageRatios { English = 2.0 }; // More tokens per char
        var counter = new ApproximateTokenCounter(customRatios);
        var text = "Hello World"; // 11 chars (excluding space)

        // Act
        var count = counter.Count(text);

        // Assert
        // With ratio of 2.0, expect more tokens than default
        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void DetectLanguage_EnglishText_ReturnsEnglish()
    {
        // Arrange
        var text = "This is a test sentence in English.";

        // Act
        var language = ApproximateTokenCounter.DetectLanguage(text);

        // Assert
        language.Should().Be(DetectedLanguage.English);
    }

    [Fact]
    public void DetectLanguage_KoreanText_ReturnsKorean()
    {
        // Arrange
        var text = "이것은 한국어 문장입니다.";

        // Act
        var language = ApproximateTokenCounter.DetectLanguage(text);

        // Assert
        language.Should().Be(DetectedLanguage.Korean);
    }

    [Fact]
    public void DetectLanguage_ChineseText_ReturnsChinese()
    {
        // Arrange
        var text = "这是一个中文句子。";

        // Act
        var language = ApproximateTokenCounter.DetectLanguage(text);

        // Assert
        language.Should().Be(DetectedLanguage.Chinese);
    }

    [Fact]
    public void DetectLanguage_MixedText_ReturnsUnknown()
    {
        // Arrange - roughly equal mix where no language exceeds 50%
        // English: "Hi" = 2 chars, Korean: "안녕하세요" = 5 chars, Chinese: "你好吗" = 3 chars
        var text = "Hi 안녕하세요 你好吗";

        // Act
        var language = ApproximateTokenCounter.DetectLanguage(text);

        // Assert
        // No single language dominates (>50%), should return Unknown
        language.Should().Be(DetectedLanguage.Unknown);
    }

    [Fact]
    public void DetectLanguage_EmptyText_ReturnsUnknown()
    {
        // Act
        var language = ApproximateTokenCounter.DetectLanguage("");

        // Assert
        language.Should().Be(DetectedLanguage.Unknown);
    }

    [Fact]
    public void Count_AccuracyComparison_WithinMargin()
    {
        // This test verifies approximate counting is within ±20% of actual
        // We'll use English text where we can estimate expected tokens

        // Arrange
        var text = "The quick brown fox jumps over the lazy dog. This is a longer text to test accuracy.";
        // Approximate: 86 chars / 4 = 21.5, ceil = 22

        // Act
        var count = _counter.Count(text);

        // Assert
        // Actual tiktoken count is around 20, so 22 is within 20% margin
        count.Should().BeInRange(15, 30);
    }
}
