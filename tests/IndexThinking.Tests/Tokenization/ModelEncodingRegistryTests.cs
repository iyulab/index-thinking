using FluentAssertions;
using IndexThinking.Tokenization;
using Xunit;

namespace IndexThinking.Tests.Tokenization;

public class ModelEncodingRegistryTests
{
    [Theory]
    [InlineData("gpt-4o")]
    [InlineData("gpt-4o-mini")]
    [InlineData("gpt-4o-2024-05-13")]
    [InlineData("chatgpt-4o-latest")]
    [InlineData("o1")]
    [InlineData("o1-mini")]
    [InlineData("o1-preview")]
    [InlineData("o3")]
    [InlineData("o3-mini")]
    [InlineData("o4-mini")]
    public void GetEncoding_O200kModels_ReturnsO200kBase(string modelId)
    {
        // Act
        var encoding = ModelEncodingRegistry.GetEncoding(modelId);

        // Assert
        encoding.Should().Be(ModelEncodingRegistry.O200kBase);
    }

    [Theory]
    [InlineData("gpt-4")]
    [InlineData("gpt-4-turbo")]
    [InlineData("gpt-4-32k")]
    [InlineData("gpt-3.5-turbo")]
    [InlineData("gpt-3.5-turbo-16k")]
    [InlineData("gpt-35-turbo")] // Azure naming
    public void GetEncoding_Cl100kModels_ReturnsCl100kBase(string modelId)
    {
        // Act
        var encoding = ModelEncodingRegistry.GetEncoding(modelId);

        // Assert
        encoding.Should().Be(ModelEncodingRegistry.Cl100kBase);
    }

    [Theory]
    [InlineData("claude-3-opus")]
    [InlineData("claude-3.5-sonnet")]
    [InlineData("gemini-pro")]
    [InlineData("gemini-1.5-pro")]
    [InlineData("deepseek-coder")]
    [InlineData("llama-3")]
    [InlineData("unknown-model")]
    public void GetEncoding_NonOpenAIModels_ReturnsNull(string modelId)
    {
        // Act
        var encoding = ModelEncodingRegistry.GetEncoding(modelId);

        // Assert
        encoding.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetEncoding_InvalidInput_ReturnsNull(string? modelId)
    {
        // Act
        var encoding = ModelEncodingRegistry.GetEncoding(modelId!);

        // Assert
        encoding.Should().BeNull();
    }

    [Theory]
    [InlineData("GPT-4O")] // Uppercase
    [InlineData("Gpt-4o")] // Mixed case
    [InlineData("gpt-4O")] // Partial uppercase
    public void GetEncoding_CaseInsensitive_ReturnsEncoding(string modelId)
    {
        // Act
        var encoding = ModelEncodingRegistry.GetEncoding(modelId);

        // Assert
        encoding.Should().Be(ModelEncodingRegistry.O200kBase);
    }

    [Theory]
    [InlineData("gpt-4o")]
    [InlineData("gpt-4")]
    [InlineData("o1-mini")]
    public void IsOpenAIModel_OpenAIModels_ReturnsTrue(string modelId)
    {
        // Act
        var result = ModelEncodingRegistry.IsOpenAIModel(modelId);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("claude-3-opus")]
    [InlineData("gemini-pro")]
    [InlineData("llama-3")]
    public void IsOpenAIModel_NonOpenAIModels_ReturnsFalse(string modelId)
    {
        // Act
        var result = ModelEncodingRegistry.IsOpenAIModel(modelId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetModelsForEncoding_O200kBase_ReturnsModels()
    {
        // Act
        var models = ModelEncodingRegistry.GetModelsForEncoding(ModelEncodingRegistry.O200kBase);

        // Assert
        models.Should().NotBeEmpty();
        models.Should().Contain("gpt-4o");
        models.Should().Contain("o1");
    }

    [Fact]
    public void GetModelsForEncoding_Cl100kBase_ReturnsModels()
    {
        // Act
        var models = ModelEncodingRegistry.GetModelsForEncoding(ModelEncodingRegistry.Cl100kBase);

        // Assert
        models.Should().NotBeEmpty();
        models.Should().Contain("gpt-4");
        models.Should().Contain("gpt-3.5-turbo");
    }

    [Fact]
    public void GetModelsForEncoding_Unknown_ReturnsEmpty()
    {
        // Act
        var models = ModelEncodingRegistry.GetModelsForEncoding("unknown_encoding");

        // Assert
        models.Should().BeEmpty();
    }

    [Theory]
    [InlineData("gpt-4o-2024-12-01")] // Future version
    [InlineData("gpt-4-custom-suffix")]
    public void GetEncoding_PrefixMatching_Works(string modelId)
    {
        // Act
        var encoding = ModelEncodingRegistry.GetEncoding(modelId);

        // Assert
        // These should match based on prefix patterns
        encoding.Should().NotBeNull();
    }
}
