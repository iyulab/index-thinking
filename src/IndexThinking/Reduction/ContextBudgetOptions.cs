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
    /// Shortens each tool result over its limit to its first and last lines (default: 30 000 characters, 50 + 20 lines).
    /// <c>null</c> leaves results alone.
    /// </summary>
    public ToolResultTrimmer? ToolResults { get; init; } = new();

    /// <summary>The counter that measures messages. Default: <see cref="Tokenization.ApproximateTokenCounter"/>.</summary>
    public ITokenCounter? TokenCounter { get; init; }
}
