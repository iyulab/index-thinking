using IndexThinking.Abstractions;

namespace IndexThinking.Reduction;

/// <summary>Settings of <see cref="ContextBudgetChatReducer"/>.</summary>
public sealed record ContextBudgetOptions
{
    /// <summary>
    /// Most tokens the request's messages may take — usually the model's context window less the room the answer needs
    /// (for example 0.8 of the window). Required; must be positive.
    /// </summary>
    public required int MaxInputTokens { get; init; }

    /// <summary>
    /// Longest tool result, in characters, sent whole; a longer one keeps its head and tail with a marker between.
    /// Default 30 000. <c>null</c> leaves results alone.
    /// </summary>
    public int? MaxToolResultChars { get; init; } = 30_000;

    /// <summary>The counter that measures messages. Default: <see cref="Tokenization.ApproximateTokenCounter"/>.</summary>
    public ITokenCounter? TokenCounter { get; init; }
}
