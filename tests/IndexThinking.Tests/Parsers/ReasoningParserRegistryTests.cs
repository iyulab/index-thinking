using FluentAssertions;
using IndexThinking.Abstractions;
using IndexThinking.Core;
using IndexThinking.Parsers;
using Microsoft.Extensions.AI;
using Moq;
using Xunit;

namespace IndexThinking.Tests.Parsers;

public class ReasoningParserRegistryTests
{
    [Fact]
    public void Default_ContainsOpenAIParser()
    {
        // Arrange
        var registry = ReasoningParserRegistry.Default;

        // Act
        var parser = registry.GetByProvider("openai");

        // Assert
        parser.Should().NotBeNull();
        parser.Should().BeOfType<OpenAIReasoningParser>();
    }

    [Fact]
    public void Default_ContainsAnthropicParser()
    {
        // Arrange
        var registry = ReasoningParserRegistry.Default;

        // Act
        var parser = registry.GetByProvider("anthropic");

        // Assert
        parser.Should().NotBeNull();
        parser.Should().BeOfType<AnthropicReasoningParser>();
    }

    [Fact]
    public void Default_ContainsGeminiParser()
    {
        // Arrange
        var registry = ReasoningParserRegistry.Default;

        // Act
        var parser = registry.GetByProvider("gemini");

        // Assert
        parser.Should().NotBeNull();
        parser.Should().BeOfType<GeminiReasoningParser>();
    }

    [Fact]
    public void Default_ContainsOpenSourceParser()
    {
        // Arrange
        var registry = ReasoningParserRegistry.Default;

        // Act
        var parser = registry.GetByProvider("opensource");

        // Assert
        parser.Should().NotBeNull();
        parser.Should().BeOfType<OpenSourceReasoningParser>();
    }

    [Fact]
    public void Register_CustomParser_CanBeRetrieved()
    {
        // Arrange
        var registry = new ReasoningParserRegistry();
        var mockParser = new Mock<IReasoningParser>();
        mockParser.Setup(p => p.ProviderFamily).Returns("custom");

        // Act
        registry.Register(mockParser.Object);
        var retrieved = registry.GetByProvider("custom");

        // Assert
        retrieved.Should().BeSameAs(mockParser.Object);
    }

    [Fact]
    public void Register_NullParser_ThrowsArgumentNullException()
    {
        // Arrange
        var registry = new ReasoningParserRegistry();

        // Act
        var action = () => registry.Register(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetByProvider_UnknownProvider_ReturnsNull()
    {
        // Arrange
        var registry = new ReasoningParserRegistry();

        // Act
        var parser = registry.GetByProvider("unknown");

        // Assert
        parser.Should().BeNull();
    }

    [Fact]
    public void GetByProvider_CaseInsensitive()
    {
        // Arrange
        var registry = ReasoningParserRegistry.Default;

        // Act
        var parser1 = registry.GetByProvider("OPENAI");
        var parser2 = registry.GetByProvider("OpenAI");
        var parser3 = registry.GetByProvider("openai");

        // Assert
        parser1.Should().NotBeNull();
        parser2.Should().NotBeNull();
        parser3.Should().NotBeNull();
        parser1.Should().BeSameAs(parser2);
        parser2.Should().BeSameAs(parser3);
    }

    [Theory]
    [InlineData("gpt-4o", "openai")]
    [InlineData("gpt-4", "openai")]
    [InlineData("gpt-3.5-turbo", "openai")]
    [InlineData("o1-mini", "openai")]
    [InlineData("o1-preview", "openai")]
    [InlineData("o3-mini", "openai")]
    [InlineData("o4-mini", "openai")]
    [InlineData("claude-3-opus", "anthropic")]
    [InlineData("claude-3.5-sonnet", "anthropic")]
    [InlineData("claude-sonnet-4", "anthropic")]
    [InlineData("gemini-pro", "gemini")]
    [InlineData("gemini-1.5-flash", "gemini")]
    [InlineData("gemini-2.5-pro", "gemini")]
    [InlineData("models/gemini-pro", "gemini")]
    [InlineData("deepseek-r1", "opensource")]
    [InlineData("deepseek-coder", "opensource")]
    [InlineData("qwen-2.5", "opensource")]
    [InlineData("qwq-32b", "opensource")]
    [InlineData("glm-4", "opensource")]
    public void GetByModel_KnownModels_ReturnsCorrectParser(string modelId, string expectedProvider)
    {
        // Arrange
        var registry = ReasoningParserRegistry.Default;

        // Act
        var parser = registry.GetByModel(modelId);

        // Assert
        parser.Should().NotBeNull();
        parser!.ProviderFamily.Should().Be(expectedProvider);
    }

    [Theory]
    [InlineData("llama-3")]
    [InlineData("mistral-7b")]
    [InlineData("unknown-model")]
    public void GetByModel_UnknownModels_ReturnsNull(string modelId)
    {
        // Arrange
        var registry = ReasoningParserRegistry.Default;

        // Act
        var parser = registry.GetByModel(modelId);

        // Assert
        parser.Should().BeNull();
    }

    [Fact]
    public void GetByModel_NullModelId_ReturnsDefault()
    {
        // Arrange
        var registry = new ReasoningParserRegistry();
        var defaultParser = new OpenAIReasoningParser();
        registry.SetDefaultParser(defaultParser);

        // Act
        var parser = registry.GetByModel(null);

        // Assert
        parser.Should().BeSameAs(defaultParser);
    }

    [Fact]
    public void SetDefaultParser_UsedAsFallback()
    {
        // Arrange
        var registry = new ReasoningParserRegistry();
        var defaultParser = new AnthropicReasoningParser();
        registry.SetDefaultParser(defaultParser);

        // Act
        var parser = registry.GetByProvider("unknown");

        // Assert
        parser.Should().BeSameAs(defaultParser);
    }

    [Fact]
    public void SetDefaultParser_NullClearsDefault()
    {
        // Arrange
        var registry = new ReasoningParserRegistry();
        registry.SetDefaultParser(new OpenAIReasoningParser());
        registry.SetDefaultParser(null);

        // Act
        var parser = registry.GetByProvider("unknown");

        // Assert
        parser.Should().BeNull();
    }

    [Fact]
    public void RegisterModelPrefix_CustomMapping_Works()
    {
        // Arrange
        var registry = new ReasoningParserRegistry();
        var parser = new OpenAIReasoningParser();
        registry.Register(parser);
        registry.RegisterModelPrefix("custom-", "openai");

        // Act
        var retrieved = registry.GetByModel("custom-model-123");

        // Assert
        retrieved.Should().BeSameAs(parser);
    }

    [Fact]
    public void RegisterModelPrefix_EmptyPrefix_ThrowsArgumentException()
    {
        // Arrange
        var registry = new ReasoningParserRegistry();

        // Act
        var action = () => registry.RegisterModelPrefix("", "openai");

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RegisterModelPrefix_EmptyProvider_ThrowsArgumentException()
    {
        // Arrange
        var registry = new ReasoningParserRegistry();

        // Act
        var action = () => registry.RegisterModelPrefix("prefix", "");

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TryGetByProvider_Found_ReturnsTrue()
    {
        // Arrange
        var registry = ReasoningParserRegistry.Default;

        // Act
        var found = registry.TryGetByProvider("openai", out var parser);

        // Assert
        found.Should().BeTrue();
        parser.Should().NotBeNull();
    }

    [Fact]
    public void TryGetByProvider_NotFound_ReturnsFalse()
    {
        // Arrange
        var registry = new ReasoningParserRegistry();

        // Act
        var found = registry.TryGetByProvider("unknown", out var parser);

        // Assert
        found.Should().BeFalse();
        parser.Should().BeNull();
    }

    [Fact]
    public void TryGetByModel_Found_ReturnsTrue()
    {
        // Arrange
        var registry = ReasoningParserRegistry.Default;

        // Act
        var found = registry.TryGetByModel("gpt-4o", out var parser);

        // Assert
        found.Should().BeTrue();
        parser.Should().NotBeNull();
    }

    [Fact]
    public void TryGetByModel_NotFound_ReturnsFalse()
    {
        // Arrange
        var registry = new ReasoningParserRegistry();

        // Act
        var found = registry.TryGetByModel("unknown", out var parser);

        // Assert
        found.Should().BeFalse();
        parser.Should().BeNull();
    }

    [Theory]
    [InlineData("gpt-4o", "openai")]
    [InlineData("claude-3-opus", "anthropic")]
    [InlineData("o1-mini", "openai")]
    [InlineData("gemini-pro", "gemini")]
    [InlineData("deepseek-r1", "opensource")]
    [InlineData("qwen-2.5", "opensource")]
    public void DetectProvider_KnownModels_ReturnsProvider(string modelId, string expectedProvider)
    {
        // Arrange
        var registry = ReasoningParserRegistry.Default;

        // Act
        var provider = registry.DetectProvider(modelId);

        // Assert
        provider.Should().Be(expectedProvider);
    }

    [Theory]
    [InlineData("unknown-model")]
    [InlineData(null)]
    [InlineData("")]
    public void DetectProvider_UnknownOrNull_ReturnsNull(string? modelId)
    {
        // Arrange
        var registry = ReasoningParserRegistry.Default;

        // Act
        var provider = registry.DetectProvider(modelId);

        // Assert
        provider.Should().BeNull();
    }

    [Fact]
    public void RegisteredProviders_ReturnsAllProviders()
    {
        // Arrange
        var registry = ReasoningParserRegistry.Default;

        // Act
        var providers = registry.RegisteredProviders;

        // Assert
        providers.Should().Contain("openai");
        providers.Should().Contain("anthropic");
        providers.Should().Contain("gemini");
        providers.Should().Contain("opensource");
    }

    [Fact]
    public void RegisteredPrefixes_ReturnsAllPrefixes()
    {
        // Arrange
        var registry = ReasoningParserRegistry.Default;

        // Act
        var prefixes = registry.RegisteredPrefixes;

        // Assert
        prefixes.Should().Contain(p => p.Prefix == "gpt-" && p.Provider == "openai");
        prefixes.Should().Contain(p => p.Prefix == "claude" && p.Provider == "anthropic");
        prefixes.Should().Contain(p => p.Prefix == "gemini" && p.Provider == "gemini");
        prefixes.Should().Contain(p => p.Prefix == "deepseek" && p.Provider == "opensource");
        prefixes.Should().Contain(p => p.Prefix == "qwen" && p.Provider == "opensource");
    }

    [Fact]
    public void Register_OverwritesExisting_SameProvider()
    {
        // Arrange
        var registry = new ReasoningParserRegistry();
        var parser1 = new OpenAIReasoningParser();
        var parser2 = new OpenAIReasoningParser();

        // Act
        registry.Register(parser1);
        registry.Register(parser2);
        var retrieved = registry.GetByProvider("openai");

        // Assert
        retrieved.Should().BeSameAs(parser2);
    }
}
