using FluentAssertions;
using IndexThinking.Continuation;
using Xunit;

namespace IndexThinking.Tests.Continuation;

public class ContinuationConfigTests
{
    [Fact]
    public void Default_HasExpectedValues()
    {
        // Act
        var config = ContinuationConfig.Default;

        // Assert
        config.MaxContinuations.Should().Be(5);
        config.MaxTotalDuration.Should().Be(TimeSpan.FromMinutes(5));
        config.DelayBetweenContinuations.Should().Be(TimeSpan.Zero);
        config.EnableJsonRecovery.Should().BeTrue();
        config.EnableCodeBlockRecovery.Should().BeTrue();
        config.ContinuationPrompt.Should().Be("Please continue from where you left off.");
        config.IncludePreviousResponse.Should().BeTrue();
        config.MinProgressPerContinuation.Should().Be(10);
        config.ThrowOnMaxContinuations.Should().BeFalse();
    }

    [Fact]
    public void Default_IsSingleton()
    {
        // Act
        var config1 = ContinuationConfig.Default;
        var config2 = ContinuationConfig.Default;

        // Assert
        config1.Should().BeSameAs(config2);
    }

    [Fact]
    public void Config_SupportsInitSyntax()
    {
        // Arrange & Act
        var config = new ContinuationConfig
        {
            MaxContinuations = 10,
            MaxTotalDuration = TimeSpan.FromMinutes(10),
            DelayBetweenContinuations = TimeSpan.FromSeconds(1),
            EnableJsonRecovery = false,
            EnableCodeBlockRecovery = false,
            ContinuationPrompt = "Continue:",
            IncludePreviousResponse = false,
            MinProgressPerContinuation = 50,
            ThrowOnMaxContinuations = true
        };

        // Assert
        config.MaxContinuations.Should().Be(10);
        config.MaxTotalDuration.Should().Be(TimeSpan.FromMinutes(10));
        config.DelayBetweenContinuations.Should().Be(TimeSpan.FromSeconds(1));
        config.EnableJsonRecovery.Should().BeFalse();
        config.EnableCodeBlockRecovery.Should().BeFalse();
        config.ContinuationPrompt.Should().Be("Continue:");
        config.IncludePreviousResponse.Should().BeFalse();
        config.MinProgressPerContinuation.Should().Be(50);
        config.ThrowOnMaxContinuations.Should().BeTrue();
    }

    [Fact]
    public void Config_SupportsWithExpression()
    {
        // Arrange
        var original = ContinuationConfig.Default;

        // Act
        var modified = original with { MaxContinuations = 3 };

        // Assert
        modified.MaxContinuations.Should().Be(3);
        modified.MaxTotalDuration.Should().Be(original.MaxTotalDuration);
        original.MaxContinuations.Should().Be(5); // Original unchanged
    }

    [Fact]
    public void Validate_ValidConfig_DoesNotThrow()
    {
        // Arrange
        var config = ContinuationConfig.Default;

        // Act
        var act = () => config.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_NegativeMaxContinuations_Throws()
    {
        // Arrange
        var config = new ContinuationConfig { MaxContinuations = -1 };

        // Act
        var act = () => config.Validate();

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("MaxContinuations");
    }

    [Fact]
    public void Validate_NegativeMaxTotalDuration_Throws()
    {
        // Arrange
        var config = new ContinuationConfig { MaxTotalDuration = TimeSpan.FromSeconds(-1) };

        // Act
        var act = () => config.Validate();

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("MaxTotalDuration");
    }

    [Fact]
    public void Validate_NegativeMinProgress_Throws()
    {
        // Arrange
        var config = new ContinuationConfig { MinProgressPerContinuation = -1 };

        // Act
        var act = () => config.Validate();

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("MinProgressPerContinuation");
    }

    [Fact]
    public void Validate_ZeroMaxContinuations_DoesNotThrow()
    {
        // Arrange - Zero is valid (disables continuation)
        var config = new ContinuationConfig { MaxContinuations = 0 };

        // Act
        var act = () => config.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_ZeroDuration_DoesNotThrow()
    {
        // Arrange
        var config = new ContinuationConfig { MaxTotalDuration = TimeSpan.Zero };

        // Act
        var act = () => config.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Config_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var config1 = new ContinuationConfig { MaxContinuations = 5 };
        var config2 = new ContinuationConfig { MaxContinuations = 5 };
        var config3 = new ContinuationConfig { MaxContinuations = 10 };

        // Assert
        config1.Should().Be(config2);
        config1.Should().NotBe(config3);
    }

    [Fact]
    public void Config_RecordHashCode_ConsistentWithEquality()
    {
        // Arrange
        var config1 = new ContinuationConfig { MaxContinuations = 5 };
        var config2 = new ContinuationConfig { MaxContinuations = 5 };

        // Assert
        config1.GetHashCode().Should().Be(config2.GetHashCode());
    }

    [Fact]
    public void Config_CustomPromptWithPlaceholder_AllowedInConfig()
    {
        // Arrange - Config accepts placeholder syntax (usage is in implementation)
        var config = new ContinuationConfig
        {
            ContinuationPrompt = "Previous: {previous_response}\nPlease continue."
        };

        // Assert
        config.ContinuationPrompt.Should().Contain("{previous_response}");
    }

    [Fact]
    public void Config_LargeMaxContinuations_Allowed()
    {
        // Arrange - No upper limit enforced
        var config = new ContinuationConfig { MaxContinuations = 100 };

        // Act
        var act = () => config.Validate();

        // Assert
        act.Should().NotThrow();
        config.MaxContinuations.Should().Be(100);
    }

    [Fact]
    public void Config_LongDuration_Allowed()
    {
        // Arrange
        var config = new ContinuationConfig { MaxTotalDuration = TimeSpan.FromHours(1) };

        // Act
        var act = () => config.Validate();

        // Assert
        act.Should().NotThrow();
    }
}
