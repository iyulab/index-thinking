using FluentAssertions;
using IndexThinking.Client;
using Xunit;

namespace IndexThinking.Tests.Client;

public class ContextWindowExceededExceptionTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var ex = new ContextWindowExceededException(50000, 32000);

        ex.InputTokens.Should().Be(50000);
        ex.MaxContextTokens.Should().Be(32000);
        ex.Message.Should().Contain("50000");
        ex.Message.Should().Contain("32000");
    }

    [Fact]
    public void IsInvalidOperationException()
    {
        var ex = new ContextWindowExceededException(100, 50);

        ex.Should().BeAssignableTo<InvalidOperationException>();
    }
}
