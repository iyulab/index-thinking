using FluentAssertions;
using IndexThinking.Core;
using Xunit;

namespace IndexThinking.Tests.Core;

public class ThinkingContentTests
{
    [Fact]
    public void ThinkingContent_ShouldRequireText()
    {
        // Arrange & Act
        var content = new ThinkingContent { Text = "Test thinking" };

        // Assert
        content.Text.Should().Be("Test thinking");
        content.TokenCount.Should().Be(0);
        content.IsSummarized.Should().BeFalse();
    }

    [Fact]
    public void ThinkingContent_ShouldSupportAllProperties()
    {
        // Arrange & Act
        var content = new ThinkingContent
        {
            Text = "Detailed reasoning process",
            TokenCount = 150,
            IsSummarized = true
        };

        // Assert
        content.Text.Should().Be("Detailed reasoning process");
        content.TokenCount.Should().Be(150);
        content.IsSummarized.Should().BeTrue();
    }

    [Fact]
    public void ThinkingContent_WithMethod_ShouldCreateNewInstance()
    {
        // Arrange
        var original = new ThinkingContent { Text = "Original", TokenCount = 100 };

        // Act
        var modified = original with { IsSummarized = true };

        // Assert
        modified.Text.Should().Be("Original");
        modified.TokenCount.Should().Be(100);
        modified.IsSummarized.Should().BeTrue();
        original.IsSummarized.Should().BeFalse();
    }

    [Fact]
    public void ThinkingContent_EqualityComparison_ShouldWork()
    {
        // Arrange
        var content1 = new ThinkingContent { Text = "Same", TokenCount = 50 };
        var content2 = new ThinkingContent { Text = "Same", TokenCount = 50 };
        var content3 = new ThinkingContent { Text = "Different", TokenCount = 50 };

        // Assert
        content1.Should().Be(content2);
        content1.Should().NotBe(content3);
    }
}
