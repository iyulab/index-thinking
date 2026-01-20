using FluentAssertions;
using IndexThinking.Abstractions;
using IndexThinking.Modifiers;
using Microsoft.Extensions.AI;
using Moq;
using Xunit;

namespace IndexThinking.Tests.Modifiers;

public class ReasoningRequestModifierRegistryTests
{
    [Fact]
    public void Default_ContainsOpenSourceModifier()
    {
        // Arrange
        var registry = ReasoningRequestModifierRegistry.Default;

        // Act
        var modifier = registry.GetByProvider("opensource");

        // Assert
        modifier.Should().NotBeNull();
        modifier.Should().BeOfType<OpenSourceRequestModifier>();
    }

    [Fact]
    public void Register_CustomModifier_CanBeRetrieved()
    {
        // Arrange
        var registry = new ReasoningRequestModifierRegistry();
        var mockModifier = new Mock<IReasoningRequestModifier>();
        mockModifier.Setup(m => m.ProviderFamily).Returns("custom");

        // Act
        registry.Register(mockModifier.Object);
        var retrieved = registry.GetByProvider("custom");

        // Assert
        retrieved.Should().BeSameAs(mockModifier.Object);
    }

    [Fact]
    public void Register_NullModifier_ThrowsArgumentNullException()
    {
        // Arrange
        var registry = new ReasoningRequestModifierRegistry();

        // Act
        var action = () => registry.Register(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetByProvider_UnknownProvider_ReturnsNull()
    {
        // Arrange
        var registry = new ReasoningRequestModifierRegistry();

        // Act
        var modifier = registry.GetByProvider("unknown");

        // Assert
        modifier.Should().BeNull();
    }

    [Fact]
    public void GetByProvider_CaseInsensitive()
    {
        // Arrange
        var registry = ReasoningRequestModifierRegistry.Default;

        // Act
        var modifier1 = registry.GetByProvider("OPENSOURCE");
        var modifier2 = registry.GetByProvider("OpenSource");
        var modifier3 = registry.GetByProvider("opensource");

        // Assert
        modifier1.Should().NotBeNull();
        modifier2.Should().NotBeNull();
        modifier3.Should().NotBeNull();
        modifier1.Should().BeSameAs(modifier2);
        modifier2.Should().BeSameAs(modifier3);
    }

    [Theory]
    [InlineData("deepseek-r1", "opensource")]
    [InlineData("deepseek-coder", "opensource")]
    [InlineData("qwen-2.5", "opensource")]
    [InlineData("qwq-32b", "opensource")]
    [InlineData("glm-4", "opensource")]
    public void GetByModel_KnownModels_ReturnsCorrectModifier(string modelId, string expectedProvider)
    {
        // Arrange
        var registry = ReasoningRequestModifierRegistry.Default;

        // Act
        var modifier = registry.GetByModel(modelId);

        // Assert
        modifier.Should().NotBeNull();
        modifier!.ProviderFamily.Should().Be(expectedProvider);
    }

    [Theory]
    [InlineData("gpt-4o")]
    [InlineData("claude-3-opus")]
    [InlineData("gemini-pro")]
    [InlineData("llama-3")]
    [InlineData("mistral-7b")]
    public void GetByModel_ModelsNotRequiringExplicitActivation_ReturnsNull(string modelId)
    {
        // Arrange
        var registry = ReasoningRequestModifierRegistry.Default;

        // Act
        var modifier = registry.GetByModel(modelId);

        // Assert
        modifier.Should().BeNull();
    }

    [Fact]
    public void GetByModel_NullModelId_ReturnsDefault()
    {
        // Arrange
        var registry = new ReasoningRequestModifierRegistry();
        var defaultModifier = new OpenSourceRequestModifier();
        registry.SetDefaultModifier(defaultModifier);

        // Act
        var modifier = registry.GetByModel(null);

        // Assert
        modifier.Should().BeSameAs(defaultModifier);
    }

    [Fact]
    public void SetDefaultModifier_UsedAsFallback()
    {
        // Arrange
        var registry = new ReasoningRequestModifierRegistry();
        var defaultModifier = new OpenSourceRequestModifier();
        registry.SetDefaultModifier(defaultModifier);

        // Act
        var modifier = registry.GetByProvider("unknown");

        // Assert
        modifier.Should().BeSameAs(defaultModifier);
    }

    [Fact]
    public void SetDefaultModifier_NullClearsDefault()
    {
        // Arrange
        var registry = new ReasoningRequestModifierRegistry();
        registry.SetDefaultModifier(new OpenSourceRequestModifier());
        registry.SetDefaultModifier(null);

        // Act
        var modifier = registry.GetByProvider("unknown");

        // Assert
        modifier.Should().BeNull();
    }

    [Fact]
    public void RegisterModelPrefix_CustomMapping_Works()
    {
        // Arrange
        var registry = new ReasoningRequestModifierRegistry();
        var modifier = new OpenSourceRequestModifier();
        registry.Register(modifier);
        registry.RegisterModelPrefix("custom-", "opensource");

        // Act
        var retrieved = registry.GetByModel("custom-model-123");

        // Assert
        retrieved.Should().BeSameAs(modifier);
    }

    [Fact]
    public void RegisterModelPrefix_EmptyPrefix_ThrowsArgumentException()
    {
        // Arrange
        var registry = new ReasoningRequestModifierRegistry();

        // Act
        var action = () => registry.RegisterModelPrefix("", "opensource");

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RegisterModelPrefix_EmptyProvider_ThrowsArgumentException()
    {
        // Arrange
        var registry = new ReasoningRequestModifierRegistry();

        // Act
        var action = () => registry.RegisterModelPrefix("prefix", "");

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("deepseek-r1")]
    [InlineData("deepseek-coder")]
    [InlineData("qwen-2.5")]
    [InlineData("qwq-32b")]
    public void RequiresExplicitActivation_OpenSourceModels_ReturnsTrue(string modelId)
    {
        // Arrange
        var registry = ReasoningRequestModifierRegistry.Default;

        // Act
        var requires = registry.RequiresExplicitActivation(modelId);

        // Assert
        requires.Should().BeTrue();
    }

    [Theory]
    [InlineData("gpt-4o")]
    [InlineData("claude-3-opus")]
    [InlineData("gemini-pro")]
    [InlineData("llama-3")]
    public void RequiresExplicitActivation_OtherModels_ReturnsFalse(string modelId)
    {
        // Arrange
        var registry = ReasoningRequestModifierRegistry.Default;

        // Act
        var requires = registry.RequiresExplicitActivation(modelId);

        // Assert
        requires.Should().BeFalse();
    }

    [Theory]
    [InlineData("deepseek-r1", "opensource")]
    [InlineData("qwen-2.5", "opensource")]
    public void DetectProvider_KnownModels_ReturnsProvider(string modelId, string expectedProvider)
    {
        // Arrange
        var registry = ReasoningRequestModifierRegistry.Default;

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
        var registry = ReasoningRequestModifierRegistry.Default;

        // Act
        var provider = registry.DetectProvider(modelId);

        // Assert
        provider.Should().BeNull();
    }

    [Fact]
    public void RegisteredProviders_ReturnsAllProviders()
    {
        // Arrange
        var registry = ReasoningRequestModifierRegistry.Default;

        // Act
        var providers = registry.RegisteredProviders;

        // Assert
        providers.Should().Contain("opensource");
    }

    [Fact]
    public void RegisteredPrefixes_ReturnsAllPrefixes()
    {
        // Arrange
        var registry = ReasoningRequestModifierRegistry.Default;

        // Act
        var prefixes = registry.RegisteredPrefixes;

        // Assert
        prefixes.Should().Contain(p => p.Prefix == "deepseek" && p.Provider == "opensource");
        prefixes.Should().Contain(p => p.Prefix == "qwen" && p.Provider == "opensource");
        prefixes.Should().Contain(p => p.Prefix == "qwq" && p.Provider == "opensource");
        prefixes.Should().Contain(p => p.Prefix == "glm" && p.Provider == "opensource");
    }

    [Fact]
    public void TryGetByProvider_Found_ReturnsTrue()
    {
        // Arrange
        var registry = ReasoningRequestModifierRegistry.Default;

        // Act
        var found = registry.TryGetByProvider("opensource", out var modifier);

        // Assert
        found.Should().BeTrue();
        modifier.Should().NotBeNull();
    }

    [Fact]
    public void TryGetByProvider_NotFound_ReturnsFalse()
    {
        // Arrange
        var registry = new ReasoningRequestModifierRegistry();

        // Act
        var found = registry.TryGetByProvider("unknown", out var modifier);

        // Assert
        found.Should().BeFalse();
        modifier.Should().BeNull();
    }

    [Fact]
    public void TryGetByModel_Found_ReturnsTrue()
    {
        // Arrange
        var registry = ReasoningRequestModifierRegistry.Default;

        // Act
        var found = registry.TryGetByModel("deepseek-r1", out var modifier);

        // Assert
        found.Should().BeTrue();
        modifier.Should().NotBeNull();
    }

    [Fact]
    public void TryGetByModel_NotFound_ReturnsFalse()
    {
        // Arrange
        var registry = new ReasoningRequestModifierRegistry();

        // Act
        var found = registry.TryGetByModel("gpt-4o", out var modifier);

        // Assert
        found.Should().BeFalse();
        modifier.Should().BeNull();
    }
}
