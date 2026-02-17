using FluentAssertions;
using IndexThinking.Tokenization;
using Xunit;

namespace IndexThinking.Tests.Tokenization;

public class LanguageRatiosTests
{
    [Fact]
    public void DefaultRatios_ShouldHaveExpectedValues()
    {
        var ratios = LanguageRatios.DefaultRatios;

        ratios.English.Should().Be(4.0);
        ratios.Korean.Should().Be(1.5);
        ratios.Japanese.Should().Be(1.5);
        ratios.Chinese.Should().Be(1.2);
        ratios.Default.Should().Be(3.5);
    }

    [Fact]
    public void DefaultRatios_ShouldReturnSameInstance()
    {
        var ratios1 = LanguageRatios.DefaultRatios;
        var ratios2 = LanguageRatios.DefaultRatios;

        ratios1.Should().BeSameAs(ratios2);
    }

    [Theory]
    [InlineData(DetectedLanguage.English, 4.0)]
    [InlineData(DetectedLanguage.Korean, 1.5)]
    [InlineData(DetectedLanguage.Japanese, 1.5)]
    [InlineData(DetectedLanguage.Chinese, 1.2)]
    [InlineData(DetectedLanguage.Unknown, 3.5)]
    public void GetRatio_ShouldReturnCorrectRatio(DetectedLanguage language, double expected)
    {
        var ratios = LanguageRatios.DefaultRatios;

        ratios.GetRatio(language).Should().Be(expected);
    }

    [Fact]
    public void CustomRatios_ShouldOverrideDefaults()
    {
        var ratios = new LanguageRatios
        {
            English = 3.0,
            Korean = 2.0,
            Japanese = 2.0,
            Chinese = 1.5,
            Default = 3.0
        };

        ratios.GetRatio(DetectedLanguage.English).Should().Be(3.0);
        ratios.GetRatio(DetectedLanguage.Korean).Should().Be(2.0);
        ratios.GetRatio(DetectedLanguage.Chinese).Should().Be(1.5);
    }

    [Fact]
    public void LanguageRatios_ShouldBeImmutableRecord()
    {
        var original = new LanguageRatios { English = 4.0, Korean = 1.5 };

        var modified = original with { Korean = 2.0 };

        original.Korean.Should().Be(1.5);
        modified.Korean.Should().Be(2.0);
        modified.English.Should().Be(4.0);
    }

    [Fact]
    public void LanguageRatios_Equality_SameValues_ShouldBeEqual()
    {
        var ratios1 = new LanguageRatios();
        var ratios2 = new LanguageRatios();

        ratios1.Should().Be(ratios2);
    }

    [Fact]
    public void LanguageRatios_Equality_DifferentValues_ShouldNotBeEqual()
    {
        var ratios1 = new LanguageRatios { English = 4.0 };
        var ratios2 = new LanguageRatios { English = 3.0 };

        ratios1.Should().NotBe(ratios2);
    }

    [Fact]
    public void GetRatio_InvalidEnumValue_ShouldReturnDefault()
    {
        var ratios = LanguageRatios.DefaultRatios;

        ratios.GetRatio((DetectedLanguage)999).Should().Be(3.5);
    }
}

public class DetectedLanguageEnumTests
{
    [Fact]
    public void DetectedLanguage_ShouldHaveFiveValues()
    {
        Enum.GetValues<DetectedLanguage>().Should().HaveCount(5);
    }

    [Theory]
    [InlineData(DetectedLanguage.Unknown, 0)]
    [InlineData(DetectedLanguage.English, 1)]
    [InlineData(DetectedLanguage.Korean, 2)]
    [InlineData(DetectedLanguage.Japanese, 3)]
    [InlineData(DetectedLanguage.Chinese, 4)]
    public void DetectedLanguage_ShouldHaveExpectedIntValues(DetectedLanguage language, int expected)
    {
        ((int)language).Should().Be(expected);
    }
}
