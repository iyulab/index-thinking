using FluentAssertions;
using IndexThinking.Core;
using Xunit;

namespace IndexThinking.Tests.Core;

public class TruncationInfoTests
{
    [Fact]
    public void TruncationInfo_NotTruncated_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var info = TruncationInfo.NotTruncated;

        // Assert
        info.IsTruncated.Should().BeFalse();
        info.Reason.Should().Be(TruncationReason.None);
        info.Details.Should().BeNull();
    }

    [Fact]
    public void TruncationInfo_ShouldSupportTokenLimit()
    {
        // Arrange & Act
        var info = new TruncationInfo
        {
            IsTruncated = true,
            Reason = TruncationReason.TokenLimit,
            Details = "Exceeded 4096 token limit"
        };

        // Assert
        info.IsTruncated.Should().BeTrue();
        info.Reason.Should().Be(TruncationReason.TokenLimit);
        info.Details.Should().Be("Exceeded 4096 token limit");
    }

    [Fact]
    public void TruncationInfo_ShouldSupportStructuralReasons()
    {
        // Arrange
        var reasons = new[]
        {
            TruncationReason.UnbalancedStructure,
            TruncationReason.IncompleteCodeBlock,
            TruncationReason.MidSentence
        };

        // Act & Assert
        foreach (var reason in reasons)
        {
            var info = new TruncationInfo
            {
                IsTruncated = true,
                Reason = reason
            };

            info.IsTruncated.Should().BeTrue();
            info.Reason.Should().Be(reason);
        }
    }

    [Fact]
    public void TruncationReason_ShouldHaveAllExpectedValues()
    {
        // Assert - 9 values: None, TokenLimit, UnbalancedStructure, IncompleteCodeBlock,
        // MidSentence, ContentFiltered, Recitation, Refusal, ContextWindowExceeded
        Enum.GetValues<TruncationReason>().Should().HaveCount(9);
        Enum.IsDefined(TruncationReason.None).Should().BeTrue();
        Enum.IsDefined(TruncationReason.TokenLimit).Should().BeTrue();
        Enum.IsDefined(TruncationReason.UnbalancedStructure).Should().BeTrue();
        Enum.IsDefined(TruncationReason.IncompleteCodeBlock).Should().BeTrue();
        Enum.IsDefined(TruncationReason.MidSentence).Should().BeTrue();
        Enum.IsDefined(TruncationReason.ContentFiltered).Should().BeTrue();
        Enum.IsDefined(TruncationReason.Recitation).Should().BeTrue();
        Enum.IsDefined(TruncationReason.Refusal).Should().BeTrue();
        Enum.IsDefined(TruncationReason.ContextWindowExceeded).Should().BeTrue();
    }

    [Fact]
    public void TruncationInfo_ShouldSupportProviderSpecificReasons()
    {
        // Arrange - Provider-specific truncation reasons
        var reasons = new[]
        {
            TruncationReason.ContentFiltered,      // OpenAI content_filter, Google SAFETY
            TruncationReason.Recitation,           // Google RECITATION
            TruncationReason.Refusal,              // Anthropic refusal
            TruncationReason.ContextWindowExceeded // Anthropic model_context_window_exceeded
        };

        // Act & Assert
        foreach (var reason in reasons)
        {
            var info = TruncationInfo.Truncated(reason, $"Provider-specific: {reason}");

            info.IsTruncated.Should().BeTrue();
            info.Reason.Should().Be(reason);
            info.Details.Should().Contain("Provider-specific");
        }
    }
}
