using FluentAssertions;
using IndexThinking.Continuation;
using Xunit;

namespace IndexThinking.Tests.Continuation;

public class ContentRecoveryUtilsTests
{
    #region TryRecoverJson Tests

    [Fact]
    public void TryRecoverJson_ValidJson_ReturnsNoRecoveryNeeded()
    {
        // Arrange
        var json = """{"name": "test", "value": 42}""";

        // Act
        var result = ContentRecoveryUtils.TryRecoverJson(json);

        // Assert
        result.Status.Should().Be(ContentRecoveryStatus.NoRecoveryNeeded);
        result.IsSuccess.Should().BeTrue();
        result.Content.Should().Be(json);
    }

    [Fact]
    public void TryRecoverJson_MissingClosingBrace_RecoversByAdding()
    {
        // Arrange
        var json = """{"name": "test", "value": 42""";

        // Act
        var result = ContentRecoveryUtils.TryRecoverJson(json);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Status.Should().Be(ContentRecoveryStatus.Recovered);
        result.Content.Should().EndWith("}");
    }

    [Fact]
    public void TryRecoverJson_MissingClosingBracket_RecoversByAdding()
    {
        // Arrange
        var json = """[1, 2, 3""";

        // Act
        var result = ContentRecoveryUtils.TryRecoverJson(json);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Content.Should().EndWith("]");
    }

    [Fact]
    public void TryRecoverJson_NestedStructure_RecoversMissingClosures()
    {
        // Arrange
        var json = """{"outer": {"inner": [1, 2, 3""";

        // Act
        var result = ContentRecoveryUtils.TryRecoverJson(json);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Content.Should().Contain("]");
        result.Content.Should().Contain("}}");
    }

    [Fact]
    public void TryRecoverJson_UnclosedString_ClosesString()
    {
        // Arrange
        var json = """{"name": "incomplete string""";

        // Act
        var result = ContentRecoveryUtils.TryRecoverJson(json);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void TryRecoverJson_NullOrEmpty_ReturnsFailed()
    {
        // Act
        var nullResult = ContentRecoveryUtils.TryRecoverJson(null!);
        var emptyResult = ContentRecoveryUtils.TryRecoverJson("");
        var whitespaceResult = ContentRecoveryUtils.TryRecoverJson("   ");

        // Assert
        nullResult.Status.Should().Be(ContentRecoveryStatus.Failed);
        emptyResult.Status.Should().Be(ContentRecoveryStatus.Failed);
        whitespaceResult.Status.Should().Be(ContentRecoveryStatus.Failed);
    }

    [Fact]
    public void TryRecoverJson_CompletelyInvalid_ReturnsFailed()
    {
        // Arrange
        var json = "this is not json at all {{{";

        // Act
        var result = ContentRecoveryUtils.TryRecoverJson(json);

        // Assert
        result.Status.Should().Be(ContentRecoveryStatus.Failed);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void TryRecoverJson_TruncatedArray_TriesToRecover()
    {
        // Arrange
        var json = """[{"id": 1}, {"id": 2}, {"id":""";

        // Act
        var result = ContentRecoveryUtils.TryRecoverJson(json);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region TryRecoverCodeBlocks Tests

    [Fact]
    public void TryRecoverCodeBlocks_NoCodeBlocks_ReturnsNoRecoveryNeeded()
    {
        // Arrange
        var text = "This is plain text without code blocks.";

        // Act
        var result = ContentRecoveryUtils.TryRecoverCodeBlocks(text);

        // Assert
        result.Status.Should().Be(ContentRecoveryStatus.NoRecoveryNeeded);
        result.Content.Should().Be(text);
    }

    [Fact]
    public void TryRecoverCodeBlocks_ClosedCodeBlock_ReturnsNoRecoveryNeeded()
    {
        // Arrange
        var text = "```csharp\nvar x = 1;\n```";

        // Act
        var result = ContentRecoveryUtils.TryRecoverCodeBlocks(text);

        // Assert
        result.Status.Should().Be(ContentRecoveryStatus.NoRecoveryNeeded);
    }

    [Fact]
    public void TryRecoverCodeBlocks_UnclosedCodeBlock_AddsClosing()
    {
        // Arrange
        var text = "Here's the code:\n```csharp\nvar x = 1;\n";

        // Act
        var result = ContentRecoveryUtils.TryRecoverCodeBlocks(text);

        // Assert
        result.Status.Should().Be(ContentRecoveryStatus.Recovered);
        result.Content.TrimEnd().Should().EndWith("```");
        result.Description.Should().Contain("1 code block");
    }

    [Fact]
    public void TryRecoverCodeBlocks_MultipleUnclosed_ClosesAll()
    {
        // Arrange
        var text = "```js\ncode1\n```\n```python\ncode2\n";

        // Act
        var result = ContentRecoveryUtils.TryRecoverCodeBlocks(text);

        // Assert
        result.Status.Should().Be(ContentRecoveryStatus.Recovered);
        result.Description.Should().Contain("1 code block");
    }

    [Fact]
    public void TryRecoverCodeBlocks_NullOrEmpty_ReturnsNoRecoveryNeeded()
    {
        // Act
        var nullResult = ContentRecoveryUtils.TryRecoverCodeBlocks(null!);
        var emptyResult = ContentRecoveryUtils.TryRecoverCodeBlocks("");

        // Assert
        nullResult.Status.Should().Be(ContentRecoveryStatus.NoRecoveryNeeded);
        emptyResult.Status.Should().Be(ContentRecoveryStatus.NoRecoveryNeeded);
    }

    [Fact]
    public void TryRecoverCodeBlocks_LanguageMarker_Preserved()
    {
        // Arrange - Verify detection works with various language markers
        var languages = new[] { "csharp", "python", "javascript", "cpp", "rust" };

        foreach (var lang in languages)
        {
            var text = $"```{lang}\ncode here";

            // Act
            var result = ContentRecoveryUtils.TryRecoverCodeBlocks(text);

            // Assert
            result.Status.Should().Be(ContentRecoveryStatus.Recovered);
        }
    }

    #endregion

    #region FindCleanTruncationPoint Tests

    [Fact]
    public void FindCleanTruncationPoint_EndsWithPeriod_ReturnsAfterPeriod()
    {
        // Arrange
        var text = "First sentence. Second incomplete";

        // Act
        var point = ContentRecoveryUtils.FindCleanTruncationPoint(text);

        // Assert
        point.Should().BeGreaterThan(0);
        text[..point].Should().Contain("First sentence.");
    }

    [Fact]
    public void FindCleanTruncationPoint_EndsWithExclamation_ReturnsAfterExclamation()
    {
        // Arrange
        var text = "Hello! This is incomplete";

        // Act
        var point = ContentRecoveryUtils.FindCleanTruncationPoint(text);

        // Assert
        point.Should().BeGreaterThan(0);
    }

    [Fact]
    public void FindCleanTruncationPoint_NoSentenceEnd_LooksForParagraph()
    {
        // Arrange
        var text = "First paragraph\n\nSecond incomplete";

        // Act
        var point = ContentRecoveryUtils.FindCleanTruncationPoint(text);

        // Assert
        point.Should().Be(17); // After "\n\n"
    }

    [Fact]
    public void FindCleanTruncationPoint_NoSentenceOrParagraph_LooksForLine()
    {
        // Arrange
        var text = "First line\nSecond incomplete";

        // Act
        var point = ContentRecoveryUtils.FindCleanTruncationPoint(text);

        // Assert
        point.Should().Be(11); // After "First line\n"
    }

    [Fact]
    public void FindCleanTruncationPoint_EmptyOrNull_ReturnsMinusOne()
    {
        // Act & Assert
        ContentRecoveryUtils.FindCleanTruncationPoint(null!).Should().Be(-1);
        ContentRecoveryUtils.FindCleanTruncationPoint("").Should().Be(-1);
    }

    [Fact]
    public void FindCleanTruncationPoint_NoBreakPoint_ReturnsMinusOne()
    {
        // Arrange
        var text = "continuous text without any breaks";

        // Act
        var point = ContentRecoveryUtils.FindCleanTruncationPoint(text);

        // Assert
        point.Should().Be(-1);
    }

    [Fact]
    public void FindCleanTruncationPoint_JapanesePunctuation_Recognized()
    {
        // Arrange
        var text = "日本語のテスト。未完成の文";

        // Act
        var point = ContentRecoveryUtils.FindCleanTruncationPoint(text);

        // Assert
        point.Should().BeGreaterThan(0);
    }

    #endregion

    #region CombineFragments Tests

    [Fact]
    public void CombineFragments_EmptyEnumerable_ReturnsEmptyString()
    {
        // Act
        var result = ContentRecoveryUtils.CombineFragments(Array.Empty<string>());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void CombineFragments_SingleFragment_ReturnsSame()
    {
        // Arrange
        var fragments = new[] { "Hello world" };

        // Act
        var result = ContentRecoveryUtils.CombineFragments(fragments);

        // Assert
        result.Should().Be("Hello world");
    }

    [Fact]
    public void CombineFragments_MultipleFragments_CombinesWithAppropriateSpacing()
    {
        // Arrange
        var fragments = new[] { "First part", "second part" };

        // Act
        var result = ContentRecoveryUtils.CombineFragments(fragments);

        // Assert
        result.Should().Be("First part second part");
    }

    [Fact]
    public void CombineFragments_EndsWithNewline_NoExtraSpace()
    {
        // Arrange
        var fragments = new[] { "First part\n", "Second part" };

        // Act
        var result = ContentRecoveryUtils.CombineFragments(fragments);

        // Assert
        result.Should().Be("First part\nSecond part");
    }

    [Fact]
    public void CombineFragments_EndsWithPunctuation_NoExtraSpace()
    {
        // Arrange
        var fragments = new[] { "First sentence.", "Second sentence." };

        // Act
        var result = ContentRecoveryUtils.CombineFragments(fragments);

        // Assert
        result.Should().Be("First sentence.Second sentence.");
    }

    [Fact]
    public void CombineFragments_SkipsEmptyFragments()
    {
        // Arrange
        var fragments = new[] { "First", "", null!, "Second" };

        // Act
        var result = ContentRecoveryUtils.CombineFragments(fragments);

        // Assert
        result.Should().Be("First Second");
    }

    [Fact]
    public void CombineFragments_NullEnumerable_ThrowsArgumentNullException()
    {
        // Act
        var act = () => ContentRecoveryUtils.CombineFragments(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CombineFragments_SecondStartsWithNewline_NoExtraSpace()
    {
        // Arrange
        var fragments = new[] { "First", "\nSecond" };

        // Act
        var result = ContentRecoveryUtils.CombineFragments(fragments);

        // Assert
        result.Should().Be("First\nSecond");
    }

    #endregion
}

public class ContentRecoveryResultTests
{
    [Fact]
    public void NoRecoveryNeeded_HasCorrectStatus()
    {
        // Act
        var result = ContentRecoveryResult.NoRecoveryNeeded("content");

        // Assert
        result.Status.Should().Be(ContentRecoveryStatus.NoRecoveryNeeded);
        result.Content.Should().Be("content");
        result.IsSuccess.Should().BeTrue();
        result.Description.Should().BeNull();
    }

    [Fact]
    public void Recovered_HasCorrectStatus()
    {
        // Act
        var result = ContentRecoveryResult.Recovered("recovered", "Added closure");

        // Assert
        result.Status.Should().Be(ContentRecoveryStatus.Recovered);
        result.Content.Should().Be("recovered");
        result.Description.Should().Be("Added closure");
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void PartiallyRecovered_HasCorrectStatus()
    {
        // Act
        var result = ContentRecoveryResult.PartiallyRecovered("partial", "Lost some data");

        // Assert
        result.Status.Should().Be(ContentRecoveryStatus.PartiallyRecovered);
        result.Content.Should().Be("partial");
        result.Description.Should().Be("Lost some data");
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Failed_HasCorrectStatus()
    {
        // Act
        var result = ContentRecoveryResult.Failed("Cannot recover");

        // Assert
        result.Status.Should().Be(ContentRecoveryStatus.Failed);
        result.Content.Should().BeEmpty();
        result.Description.Should().Be("Cannot recover");
        result.IsSuccess.Should().BeFalse();
    }
}

public class ContentRecoveryStatusTests
{
    [Fact]
    public void ContentRecoveryStatus_HasAllExpectedValues()
    {
        // Assert
        Enum.GetValues<ContentRecoveryStatus>().Should().HaveCount(4);
        Enum.IsDefined(ContentRecoveryStatus.NoRecoveryNeeded).Should().BeTrue();
        Enum.IsDefined(ContentRecoveryStatus.Recovered).Should().BeTrue();
        Enum.IsDefined(ContentRecoveryStatus.PartiallyRecovered).Should().BeTrue();
        Enum.IsDefined(ContentRecoveryStatus.Failed).Should().BeTrue();
    }
}
