using FluentAssertions;
using IndexThinking.Continuation;
using IndexThinking.Core;
using Microsoft.Extensions.AI;
using Xunit;

namespace IndexThinking.Tests.Continuation;

public class TruncationDetectorTests
{
    private readonly TruncationDetector _detector = new();

    [Fact]
    public void Detect_NullResponse_ReturnsNotTruncated()
    {
        // Act
        var result = _detector.Detect(null!);

        // Assert
        result.IsTruncated.Should().BeFalse();
        result.Reason.Should().Be(TruncationReason.None);
    }

    [Fact]
    public void Detect_EmptyResponse_ReturnsNotTruncated()
    {
        // Arrange
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, ""));

        // Act
        var result = _detector.Detect(response);

        // Assert
        result.IsTruncated.Should().BeFalse();
    }

    [Fact]
    public void Detect_FinishReasonLength_ReturnsTruncated()
    {
        // Arrange
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Some text"))
        {
            FinishReason = ChatFinishReason.Length
        };

        // Act
        var result = _detector.Detect(response);

        // Assert
        result.IsTruncated.Should().BeTrue();
        result.Reason.Should().Be(TruncationReason.TokenLimit);
        result.Details.Should().Contain("length");
    }

    [Fact]
    public void Detect_FinishReasonStop_ReturnsNotTruncated()
    {
        // Arrange
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Complete response."))
        {
            FinishReason = ChatFinishReason.Stop
        };

        // Act
        var result = _detector.Detect(response);

        // Assert
        result.IsTruncated.Should().BeFalse();
    }

    [Fact]
    public void Detect_UnbalancedBraces_ReturnsTruncated()
    {
        // Arrange
        var text = "function test() {\n  if (x) {\n    console.log('incomplete";
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, text));

        // Act
        var result = _detector.Detect(response);

        // Assert
        result.IsTruncated.Should().BeTrue();
        result.Reason.Should().Be(TruncationReason.UnbalancedStructure);
    }

    [Fact]
    public void Detect_BalancedBraces_ReturnsNotTruncated()
    {
        // Arrange
        var text = "function test() { return 42; }";
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, text));

        // Act
        var result = _detector.Detect(response);

        // Assert
        result.IsTruncated.Should().BeFalse();
    }

    [Fact]
    public void Detect_UnclosedCodeBlock_ReturnsTruncated()
    {
        // Arrange
        var text = "Here's the code:\n```csharp\npublic class Test\n{\n";
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, text));

        // Act
        var result = _detector.Detect(response);

        // Assert
        result.IsTruncated.Should().BeTrue();
        result.Reason.Should().BeOneOf(TruncationReason.IncompleteCodeBlock, TruncationReason.UnbalancedStructure);
    }

    [Fact]
    public void Detect_ClosedCodeBlock_ReturnsNotTruncated()
    {
        // Arrange
        var text = "Here's the code:\n```csharp\npublic class Test { }\n```";
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, text));

        // Act
        var result = _detector.Detect(response);

        // Assert
        result.IsTruncated.Should().BeFalse();
    }

    [Fact]
    public void Detect_MidSentence_WithHeuristicsEnabled_ReturnsTruncated()
    {
        // Arrange
        var text = new string('a', 150) + " and then we need to"; // Long text ending mid-sentence
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, text));

        // Act
        var result = _detector.Detect(response);

        // Assert
        result.IsTruncated.Should().BeTrue();
        result.Reason.Should().Be(TruncationReason.MidSentence);
    }

    [Fact]
    public void Detect_ProperSentenceEnding_ReturnsNotTruncated()
    {
        // Arrange
        var text = new string('a', 150) + ". This is a complete sentence.";
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, text));

        // Act
        var result = _detector.Detect(response);

        // Assert
        result.IsTruncated.Should().BeFalse();
    }

    [Fact]
    public void Detect_ShortText_SkipsHeuristics()
    {
        // Arrange - Short text should skip mid-sentence check
        var text = "incomplete text without";
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, text));

        // Act
        var result = _detector.Detect(response);

        // Assert - Short text is assumed intentionally brief
        result.Reason.Should().NotBe(TruncationReason.MidSentence);
    }

    [Fact]
    public void Detect_EndsWithListMarker_ReturnsNotTruncated()
    {
        // Arrange
        var text = new string('a', 150) + "\n- Item:";
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, text));

        // Act
        var result = _detector.Detect(response);

        // Assert - List markers are valid endings
        result.Reason.Should().NotBe(TruncationReason.MidSentence);
    }

    [Fact]
    public void DetectInText_WorksWithoutResponse()
    {
        // Arrange
        var text = "function test() { // incomplete";

        // Act
        var result = _detector.DetectInText(text);

        // Assert
        result.IsTruncated.Should().BeTrue();
        result.Reason.Should().Be(TruncationReason.UnbalancedStructure);
    }

    [Fact]
    public void DetectInText_EmptyString_ReturnsNotTruncated()
    {
        // Act
        var result = _detector.DetectInText("");

        // Assert
        result.IsTruncated.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithOptions_AppliesConfiguration()
    {
        // Arrange
        var options = new TruncationDetectorOptions
        {
            EnableStructuralAnalysis = false,
            EnableHeuristicAnalysis = false
        };
        var detector = new TruncationDetector(options);

        // Arrange - Text that would normally trigger structural detection
        var text = "function() { // unbalanced";
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, text));

        // Act
        var result = detector.Detect(response);

        // Assert - Should not detect since structural analysis is disabled
        result.Reason.Should().NotBe(TruncationReason.UnbalancedStructure);
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new TruncationDetector(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Detect_StringsWithEscapedQuotes_HandlesCorrectly()
    {
        // Arrange - Escaped quotes shouldn't break brace counting
        var text = """var x = "test with \" escaped quote"; var y = {}""";
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, text));

        // Act
        var result = _detector.Detect(response);

        // Assert
        result.IsTruncated.Should().BeFalse();
    }

    [Fact]
    public void Detect_NestedBraces_CountsCorrectly()
    {
        // Arrange
        var text = "{ a: { b: { c: 1 } } }";
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, text));

        // Act
        var result = _detector.Detect(response);

        // Assert
        result.IsTruncated.Should().BeFalse();
    }

    [Fact]
    public void Detect_MultipleCodeBlocks_AllClosed_ReturnsNotTruncated()
    {
        // Arrange
        var text = "```js\ncode1\n```\n```python\ncode2\n```";
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, text));

        // Act
        var result = _detector.Detect(response);

        // Assert
        result.IsTruncated.Should().BeFalse();
    }

    [Fact]
    public void Detect_JapaneseAndChinesePunctuation_RecognizedAsEnding()
    {
        // Arrange - Japanese/Chinese period (。) and comma (、) are valid endings
        var text = new string('a', 150) + "これで完了です。";
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, text));

        // Act
        var result = _detector.Detect(response);

        // Assert
        result.IsTruncated.Should().BeFalse();
    }
}

public class TruncationDetectorOptionsTests
{
    [Fact]
    public void DefaultOptions_HaveExpectedValues()
    {
        // Arrange & Act
        var options = new TruncationDetectorOptions();

        // Assert
        options.EnableStructuralAnalysis.Should().BeTrue();
        options.EnableHeuristicAnalysis.Should().BeTrue();
        options.MinTextLengthForHeuristics.Should().Be(100);
    }

    [Fact]
    public void Options_SupportInitSyntax()
    {
        // Arrange & Act
        var options = new TruncationDetectorOptions
        {
            EnableStructuralAnalysis = false,
            EnableHeuristicAnalysis = false,
            MinTextLengthForHeuristics = 50
        };

        // Assert
        options.EnableStructuralAnalysis.Should().BeFalse();
        options.EnableHeuristicAnalysis.Should().BeFalse();
        options.MinTextLengthForHeuristics.Should().Be(50);
    }
}
