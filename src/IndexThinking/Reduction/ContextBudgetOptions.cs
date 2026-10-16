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

    /// <summary>
    /// Over the budget, masks old tool results before any message is dropped (default: results outside the newest three user
    /// turns, 200 characters or longer). <c>null</c> skips the step.
    /// </summary>
    public ToolResultMasker? Masking { get; init; } = new();

    /// <summary>
    /// Keeps the originals of masked results so the model can read them back by id (the placeholder names the tool and the
    /// id). <c>null</c> (default): the placeholder asks for the same call again.
    /// </summary>
    public IToolResultRetention? Retention { get; init; }

    /// <summary>The counter that measures messages. Default: <see cref="Tokenization.ApproximateTokenCounter"/>.</summary>
    public ITokenCounter? TokenCounter { get; init; }
}
