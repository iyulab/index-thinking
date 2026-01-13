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
        // Assert
        Enum.GetValues<TruncationReason>().Should().HaveCount(5);
        Enum.IsDefined(TruncationReason.None).Should().BeTrue();
        Enum.IsDefined(TruncationReason.TokenLimit).Should().BeTrue();
        Enum.IsDefined(TruncationReason.UnbalancedStructure).Should().BeTrue();
        Enum.IsDefined(TruncationReason.IncompleteCodeBlock).Should().BeTrue();
        Enum.IsDefined(TruncationReason.MidSentence).Should().BeTrue();
    }
}
