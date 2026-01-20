using FluentAssertions;
using IndexThinking.Modifiers;
using Microsoft.Extensions.AI;
using Xunit;

namespace IndexThinking.Tests.Modifiers;

public class OpenSourceRequestModifierTests
{
    [Fact]
    public void ProviderFamily_ReturnsOpenSource()
    {
        // Arrange
        var modifier = new OpenSourceRequestModifier();

        // Act
        var family = modifier.ProviderFamily;

        // Assert
        family.Should().Be("opensource");
    }

    [Theory]
    [InlineData("deepseek-r1", true)]
    [InlineData("deepseek-coder", true)]
    [InlineData("DEEPSEEK-R1", true)]
    [InlineData("qwen-2.5", true)]
    [InlineData("qwq-32b", true)]
    [InlineData("glm-4", true)]
    [InlineData("my-custom-r1-model", true)]      // Contains "r1"
    [InlineData("thinking-model", true)]          // Contains "thinking"
    [InlineData("gpt-4o", false)]
    [InlineData("claude-3-opus", false)]
    [InlineData("gemini-pro", false)]
    [InlineData("llama-3", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void SupportsModel_ReturnsExpectedResult(string? modelId, bool expected)
    {
        // Arrange
        var modifier = new OpenSourceRequestModifier();

        // Act
        var supports = modifier.SupportsModel(modelId);

        // Assert
        supports.Should().Be(expected);
    }

    [Fact]
    public void EnableReasoning_NullOptions_CreatesNewOptions()
    {
        // Arrange
        var modifier = new OpenSourceRequestModifier();

        // Act
        var result = modifier.EnableReasoning(null, "deepseek-r1");

        // Assert
        result.Should().NotBeNull();
        result.AdditionalProperties.Should().ContainKey("include_reasoning");
        result.AdditionalProperties!["include_reasoning"].Should().Be(true);
    }

    [Fact]
    public void EnableReasoning_ExistingOptions_ModifiesInPlace()
    {
        // Arrange
        var modifier = new OpenSourceRequestModifier();
        var options = new ChatOptions
        {
            ModelId = "deepseek-r1",
            MaxOutputTokens = 4096
        };

        // Act
        var result = modifier.EnableReasoning(options, "deepseek-r1");

        // Assert
        result.Should().BeSameAs(options);
        result.AdditionalProperties.Should().ContainKey("include_reasoning");
        result.MaxOutputTokens.Should().Be(4096);
    }

    [Fact]
    public void EnableReasoning_DefaultSettings_AddsIncludeReasoning()
    {
        // Arrange
        var modifier = new OpenSourceRequestModifier();

        // Act
        var result = modifier.EnableReasoning(new ChatOptions(), "deepseek-r1");

        // Assert
        result.AdditionalProperties.Should().ContainKey("include_reasoning");
        result.AdditionalProperties!["include_reasoning"].Should().Be(true);
    }

    [Fact]
    public void EnableReasoning_CustomDefaultField_UsesCustomField()
    {
        // Arrange
        var settings = new OpenSourceReasoningRequestSettings
        {
            DefaultRequestField = "reasoning_enabled"
        };
        var modifier = new OpenSourceRequestModifier(settings);

        // Act
        var result = modifier.EnableReasoning(new ChatOptions(), "deepseek-r1");

        // Assert
        result.AdditionalProperties.Should().ContainKey("reasoning_enabled");
        result.AdditionalProperties!["reasoning_enabled"].Should().Be(true);
    }

    [Fact]
    public void EnableReasoning_ModelFieldOverride_UsesOverrideField()
    {
        // Arrange
        var settings = new OpenSourceReasoningRequestSettings
        {
            DefaultRequestField = "include_reasoning",
            ModelFieldOverrides = new Dictionary<string, string>
            {
                ["custom-model"] = "enable_thinking"
            }
        };
        var modifier = new OpenSourceRequestModifier(settings);

        // Act
        var result = modifier.EnableReasoning(new ChatOptions(), "custom-model-v1");

        // Assert
        result.AdditionalProperties.Should().ContainKey("enable_thinking");
        result.AdditionalProperties!["enable_thinking"].Should().Be(true);
    }

    [Fact]
    public void EnableReasoning_IncludeReasoningFormat_AddsReasoningFormat()
    {
        // Arrange
        var settings = new OpenSourceReasoningRequestSettings
        {
            IncludeReasoningFormat = true
        };
        var modifier = new OpenSourceRequestModifier(settings);

        // Act
        var result = modifier.EnableReasoning(new ChatOptions(), "deepseek-r1");

        // Assert
        result.AdditionalProperties.Should().ContainKey("include_reasoning");
        result.AdditionalProperties.Should().ContainKey("reasoning_format");
        result.AdditionalProperties!["reasoning_format"].Should().Be("parsed");
    }

    [Fact]
    public void EnableReasoning_UseAlternativeQwenField_AddsEnableThinking()
    {
        // Arrange
        var settings = new OpenSourceReasoningRequestSettings
        {
            UseAlternativeQwenField = true
        };
        var modifier = new OpenSourceRequestModifier(settings);

        // Act
        var result = modifier.EnableReasoning(new ChatOptions(), "qwen-2.5");

        // Assert
        result.AdditionalProperties.Should().ContainKey("include_reasoning");
        result.AdditionalProperties.Should().ContainKey("enable_thinking");
        result.AdditionalProperties!["enable_thinking"].Should().Be(true);
    }

    [Fact]
    public void EnableReasoning_QwqModel_AlternativeFieldApplies()
    {
        // Arrange
        var settings = new OpenSourceReasoningRequestSettings
        {
            UseAlternativeQwenField = true
        };
        var modifier = new OpenSourceRequestModifier(settings);

        // Act
        var result = modifier.EnableReasoning(new ChatOptions(), "qwq-32b");

        // Assert
        result.AdditionalProperties.Should().ContainKey("enable_thinking");
    }

    [Fact]
    public void EnableReasoning_PreservesExistingAdditionalProperties()
    {
        // Arrange
        var modifier = new OpenSourceRequestModifier();
        var options = new ChatOptions();
        options.AdditionalProperties ??= new AdditionalPropertiesDictionary();
        options.AdditionalProperties["existing_key"] = "existing_value";

        // Act
        var result = modifier.EnableReasoning(options, "deepseek-r1");

        // Assert
        result.AdditionalProperties.Should().ContainKey("existing_key");
        result.AdditionalProperties!["existing_key"].Should().Be("existing_value");
        result.AdditionalProperties.Should().ContainKey("include_reasoning");
    }

    [Fact]
    public void Constructor_NullSettings_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new OpenSourceRequestModifier(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void EnableReasoning_NonDeepseekModel_NoReasoningFormat()
    {
        // Arrange
        var settings = new OpenSourceReasoningRequestSettings
        {
            IncludeReasoningFormat = true
        };
        var modifier = new OpenSourceRequestModifier(settings);

        // Act
        var result = modifier.EnableReasoning(new ChatOptions(), "glm-4");

        // Assert
        result.AdditionalProperties.Should().ContainKey("include_reasoning");
        result.AdditionalProperties.Should().NotContainKey("reasoning_format");
    }
}
