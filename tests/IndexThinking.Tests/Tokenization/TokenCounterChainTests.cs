using FluentAssertions;
using IndexThinking.Abstractions;
using IndexThinking.Tokenization;
using Microsoft.Extensions.AI;
using Moq;
using Xunit;

namespace IndexThinking.Tests.Tokenization;

public class TokenCounterChainTests
{
    [Fact]
    public void Constructor_EmptyCounters_ThrowsArgumentException()
    {
        // Act
        var action = () => new TokenCounterChain(Array.Empty<ITokenCounter>());

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*At least one counter*");
    }

    [Fact]
    public void Constructor_NullCounters_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new TokenCounterChain(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Count_WithNoModel_UsesFirstCounter()
    {
        // Arrange
        var mockCounter1 = new Mock<ITokenCounter>();
        var mockCounter2 = new Mock<ITokenCounter>();
        mockCounter1.Setup(c => c.Count("test")).Returns(5);

        var chain = new TokenCounterChain(new[] { mockCounter1.Object, mockCounter2.Object });

        // Act
        var count = chain.Count("test");

        // Assert
        count.Should().Be(5);
        mockCounter1.Verify(c => c.Count("test"), Times.Once);
        mockCounter2.Verify(c => c.Count(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Count_WithSupportedModel_UsesFirstMatch()
    {
        // Arrange
        var mockCounter1 = new Mock<ITokenCounter>();
        var mockCounter2 = new Mock<ITokenCounter>();
        mockCounter1.Setup(c => c.SupportsModel("gpt-4o")).Returns(true);
        mockCounter1.Setup(c => c.Count("test")).Returns(10);
        mockCounter2.Setup(c => c.SupportsModel("gpt-4o")).Returns(true);
        mockCounter2.Setup(c => c.Count("test")).Returns(20);

        var chain = new TokenCounterChain(new[] { mockCounter1.Object, mockCounter2.Object })
            .WithModel("gpt-4o");

        // Act
        var count = chain.Count("test");

        // Assert
        count.Should().Be(10); // First supporting counter
    }

    [Fact]
    public void Count_WithUnsupportedModel_UsesFallback()
    {
        // Arrange
        var mockCounter1 = new Mock<ITokenCounter>();
        var mockCounter2 = new Mock<ITokenCounter>();
        mockCounter1.Setup(c => c.SupportsModel("claude-3")).Returns(false);
        mockCounter2.Setup(c => c.SupportsModel("claude-3")).Returns(true); // Fallback
        mockCounter2.Setup(c => c.Count("test")).Returns(15);

        var chain = new TokenCounterChain(new[] { mockCounter1.Object, mockCounter2.Object })
            .WithModel("claude-3");

        // Act
        var count = chain.Count("test");

        // Assert
        count.Should().Be(15);
    }

    [Fact]
    public void Count_NoCounterSupports_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockCounter = new Mock<ITokenCounter>();
        mockCounter.Setup(c => c.SupportsModel(It.IsAny<string>())).Returns(false);

        var chain = new TokenCounterChain(new[] { mockCounter.Object })
            .WithModel("unsupported-model");

        // Act
        var action = () => chain.Count("test");

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*No counter*supports*unsupported-model*");
    }

    [Fact]
    public void WithModel_ReturnsNewInstance()
    {
        // Arrange
        var chain = new TokenCounterChain(new[] { new ApproximateTokenCounter() });

        // Act
        var chainWithModel = chain.WithModel("gpt-4o");

        // Assert
        chainWithModel.Should().NotBeSameAs(chain);
        chainWithModel.ModelId.Should().Be("gpt-4o");
        chain.ModelId.Should().BeNull();
    }

    [Fact]
    public void WithModel_NullOrEmpty_ThrowsArgumentException()
    {
        // Arrange
        var chain = new TokenCounterChain(new[] { new ApproximateTokenCounter() });

        // Act & Assert
        chain.Invoking(c => c.WithModel(null!)).Should().Throw<ArgumentException>();
        chain.Invoking(c => c.WithModel("")).Should().Throw<ArgumentException>();
        chain.Invoking(c => c.WithModel("   ")).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SupportsModel_AnyCounterSupports_ReturnsTrue()
    {
        // Arrange
        var mockCounter1 = new Mock<ITokenCounter>();
        var mockCounter2 = new Mock<ITokenCounter>();
        mockCounter1.Setup(c => c.SupportsModel("test")).Returns(false);
        mockCounter2.Setup(c => c.SupportsModel("test")).Returns(true);

        var chain = new TokenCounterChain(new[] { mockCounter1.Object, mockCounter2.Object });

        // Act
        var result = chain.SupportsModel("test");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void SupportsModel_NoCounterSupports_ReturnsFalse()
    {
        // Arrange
        var mockCounter = new Mock<ITokenCounter>();
        mockCounter.Setup(c => c.SupportsModel(It.IsAny<string>())).Returns(false);

        var chain = new TokenCounterChain(new[] { mockCounter.Object });

        // Act
        var result = chain.SupportsModel("unsupported");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Counters_Property_ReturnsCounterList()
    {
        // Arrange
        var counter1 = new ApproximateTokenCounter();
        var counter2 = new ApproximateTokenCounter();
        var chain = new TokenCounterChain(new[] { counter1, counter2 });

        // Assert
        chain.Counters.Should().HaveCount(2);
        chain.Counters.Should().Contain(counter1);
        chain.Counters.Should().Contain(counter2);
    }

    [Fact]
    public void Count_ChatMessage_DelegatesToCounter()
    {
        // Arrange
        var mockCounter = new Mock<ITokenCounter>();
        var message = new ChatMessage(ChatRole.User, "Hello");
        mockCounter.Setup(c => c.SupportsModel(It.IsAny<string>())).Returns(true);
        mockCounter.Setup(c => c.Count(message)).Returns(10);

        var chain = new TokenCounterChain(new[] { mockCounter.Object })
            .WithModel("test");

        // Act
        var count = chain.Count(message);

        // Assert
        count.Should().Be(10);
    }

    [Fact]
    public void Integration_TiktokenAndApproximate_Works()
    {
        // Arrange
        var tiktoken = new TiktokenTokenCounter(ModelEncodingRegistry.O200kBase);
        var approximate = new ApproximateTokenCounter();
        var chain = new TokenCounterChain(new ITokenCounter[] { tiktoken, approximate });

        // Act - GPT-4o uses tiktoken
        var gptChain = chain.WithModel("gpt-4o");
        var gptCount = gptChain.Count("Hello World");

        // Act - Claude uses approximate
        var claudeChain = chain.WithModel("claude-3");
        var claudeCount = claudeChain.Count("Hello World");

        // Assert
        gptCount.Should().Be(2); // Exact tiktoken count
        claudeCount.Should().BeGreaterThan(0); // Approximate count
    }
}
