using FluentAssertions;
using IndexThinking.Abstractions;
using Microsoft.Extensions.AI;
using Xunit;

namespace IndexThinking.Tests.Tokenization;

/// <summary>
/// Regression teeth for the ITokenCounter ISP split (2026-07-05): the core counting
/// interface must stay framework-neutral; M.E.AI coupling lives only in
/// <see cref="IChatMessageTokenCounter"/>.
/// </summary>
public class TokenCounterInterfaceNeutralityTests
{
    [Fact]
    public void ITokenCounter_Members_Do_Not_Reference_MEAI_Types()
    {
        foreach (var method in typeof(ITokenCounter).GetMethods())
        {
            method.ReturnType.Namespace.Should().NotStartWith("Microsoft.Extensions.AI",
                $"core interface member '{method.Name}' return type must be framework-neutral");
            foreach (var parameter in method.GetParameters())
            {
                parameter.ParameterType.Namespace.Should().NotStartWith("Microsoft.Extensions.AI",
                    $"core interface member '{method.Name}' parameter '{parameter.Name}' must be framework-neutral");
            }
        }
    }

    [Fact]
    public void IChatMessageTokenCounter_Extends_Core_And_Owns_ChatMessage_Member()
    {
        typeof(IChatMessageTokenCounter).Should().Implement<ITokenCounter>();
        typeof(IChatMessageTokenCounter).GetMethod("Count", [typeof(ChatMessage)]).Should().NotBeNull();
    }

    [Fact]
    public void CountMessage_Delegates_To_ChatMessage_Counter_When_Implemented()
    {
        var counter = new IndexThinking.Tokenization.ApproximateTokenCounter();
        var message = new ChatMessage(ChatRole.User, "hello world");

        counter.CountMessage(message).Should().Be(((IChatMessageTokenCounter)counter).Count(message));
    }

    [Fact]
    public void CountMessage_Falls_Back_To_Text_Counting_For_Neutral_Counters()
    {
        var counter = new TextOnlyCounter();
        var message = new ChatMessage(ChatRole.User, "hello world");

        counter.CountMessage(message).Should().Be(
            counter.Count("hello world") + ChatMessageTokenCounterExtensions.MessageOverhead);
    }

    private sealed class TextOnlyCounter : ITokenCounter
    {
        public int Count(string text) => text.Length;
        public int Count(IEnumerable<string> texts) => texts.Sum(Count);
        public bool SupportsModel(string modelId) => true;
    }
}
