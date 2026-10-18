using AwesomeAssertions;
using IndexThinking.Tokenization;
using Xunit;

namespace IndexThinking.Tests.Tokenization;

public class TokenCounterFactoryTests
{
    private readonly TokenCounterFactory _factory = new();

    [Fact]
    public void Create_WithGpt4o_ReturnsTiktokenFirst()
    {
        // Act
        var counter = _factory.Create("gpt-4o");

        // Assert
        counter.Should().BeOfType<TokenCounterChain>();
        var chain = (TokenCounterChain)counter;
        chain.Counters.Should().HaveCountGreaterThanOrEqualTo(1);
        chain.Counters[0].Should().BeOfType<TiktokenTokenCounter>();
    }

    [Fact]
    public void Create_WithClaude_ReturnsApproximateOnly()
    {
        // Act
        var counter = _factory.Create("claude-3");

        // Assert
        counter.Should().BeOfType<TokenCounterChain>();
        var chain = (TokenCounterChain)counter;

        // Claude is not OpenAI, so tiktoken won't be added
        // Only approximate counter should be present
        chain.Counters.Should().ContainSingle()
            .Which.Should().BeOfType<ApproximateTokenCounter>();
    }

    [Fact]
    public void Create_WithNull_ReturnsDefaultChain()
    {
        // Act
        var counter = _factory.Create(null);

        // Assert
        counter.Should().BeOfType<TokenCounterChain>();
        var chain = (TokenCounterChain)counter;

        // Default chain should include both tiktoken (default encoding) and approximate
        chain.Counters.Should().HaveCountGreaterThanOrEqualTo(2);
        chain.Counters[0].Should().BeOfType<TiktokenTokenCounter>();
        chain.Counters[^1].Should().BeOfType<ApproximateTokenCounter>();
    }

    [Fact]
    public void CreateDefault_ReturnsChainWithBothCounters()
    {
        // Act
        var counter = _factory.CreateDefault();

        // Assert
        counter.Should().BeOfType<TokenCounterChain>();
        var chain = (TokenCounterChain)counter;
        chain.Counters.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void CreateApproximate_ReturnsApproximateCounter()
    {
        // Act
        var counter = _factory.CreateApproximate();

        // Assert
        counter.Should().BeOfType<ApproximateTokenCounter>();
    }

    [Fact]
    public void Constructor_WithCustomRatios_UsesCustomRatios()
    {
        // Arrange
        var customRatios = new LanguageRatios { English = 2.0 };
        var factory = new TokenCounterFactory(customRatios);

        // Act
        var counter = factory.CreateApproximate();
        var count = counter.Count("Hello World"); // 10 chars

        // Assert
        // With ratio 2.0, expect ~5 tokens
        count.Should().BeGreaterThan(3);
    }

    [Fact]
    public void Default_Property_ReturnsSharedInstance()
    {
        // Act
        var factory1 = TokenCounterFactory.Default;
        var factory2 = TokenCounterFactory.Default;

        // Assert
        factory1.Should().BeSameAs(factory2);
    }

    [Theory]
    [InlineData("gpt-4o")]
    [InlineData("gpt-4")]
    [InlineData("o1-mini")]
    public void Create_OpenAIModels_IncludesTiktoken(string modelId)
    {
        // Act
        var counter = _factory.Create(modelId);

        // Assert
        var chain = (TokenCounterChain)counter;
        chain.Counters.Should().Contain(c => c is TiktokenTokenCounter);
    }

    [Theory]
    [InlineData("claude-3-opus")]
    [InlineData("gemini-pro")]
    [InlineData("llama-3")]
    public void Create_NonOpenAIModels_DoesNotIncludeTiktoken(string modelId)
    {
        // Act
        var counter = _factory.Create(modelId);

        // Assert
        var chain = (TokenCounterChain)counter;
        chain.Counters.Should().NotContain(c => c is TiktokenTokenCounter);
    }

    [Fact]
    public void Create_ReturnsWorkingCounter()
    {
        // Arrange
        var counter = _factory.Create("gpt-4o");

        // Act
        var count = counter.Count("Hello World");

        // Assert
        count.Should().Be(2); // Tiktoken exact count
    }

    [Fact]
    public void Create_NonOpenAI_ReturnsWorkingCounter()
    {
        // Arrange
        var counter = _factory.Create("claude-3");

        // Act
        var count = counter.Count("Hello World");

        // Assert
        count.Should().BeGreaterThan(0);
    }

    private const string JsonResult = "{\"items\":[{\"id\":1042,\"path\":\"src/a.cs\",\"size\":3381},{\"id\":1043,\"path\":\"src/b.cs\",\"size\":902}],\"total\":2}";

    [Theory]
    [InlineData("gemma-4-E4B-it")]
    [InlineData("gguf:gemma4-fast")]
    [InlineData("gguf:qwen3-fast")]
    [InlineData("Qwen3-8B")]
    public void Create_ForALocalModelFamily_CountsJsonHigherThanForACloudModel(string modelId)
    {
        // Gemma's and Qwen's tokenizers split digits and punctuation finer than o200k_base; their counter must not undercount
        // a JSON tool result the way the o200k-fitted ratios do.
        var local = _factory.Create(modelId).Count(JsonResult);
        var cloud = _factory.Create("claude-sonnet-4").Count(JsonResult);

        local.Should().BeGreaterThan(cloud);
    }

    [Fact]
    public void Create_WithAnUnknownModel_KeepsTheO200kFittedRatios()
    {
        _factory.Create("claude-sonnet-4").Count(JsonResult)
            .Should().Be(new ApproximateTokenCounter(LanguageRatios.DefaultRatios).Count(JsonResult));
    }

    [Fact]
    public void A_factory_given_its_own_ratios_keeps_them_for_every_model()
    {
        var ratios = new LanguageRatios { Punctuation = 0.5 };
        var factory = new TokenCounterFactory(ratios);

        factory.Create("gguf:qwen3-fast").Count(JsonResult).Should().Be(new ApproximateTokenCounter(ratios).Count(JsonResult));
    }
}
