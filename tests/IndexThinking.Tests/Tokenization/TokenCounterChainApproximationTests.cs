using AwesomeAssertions;
using IndexThinking.Abstractions;
using IndexThinking.Tokenization;
using Xunit;

namespace IndexThinking.Tests.Tokenization;

/// <summary>
/// The chain says whether its count for a model is approximate — the answer of the counter it routes that model to.
/// Before, it fell through to <see cref="ITokenCounter.IsApproximate"/>'s default and said <c>false</c> for every model,
/// including the ones it routes to the approximate fallback.
/// </summary>
public class TokenCounterChainApproximationTests
{
    [Fact]
    public void A_model_routed_to_the_approximate_fallback_is_approximate()
    {
        ITokenCounter chain = TokenCounterFactory.Default.Create("claude-3");

        chain.IsApproximate("claude-3").Should().BeTrue();
    }

    [Fact]
    public void A_model_with_an_exact_tokenizer_is_not_approximate()
    {
        ITokenCounter chain = TokenCounterFactory.Default.Create("gpt-4o");

        chain.IsApproximate("gpt-4o").Should().BeFalse();
    }
}
