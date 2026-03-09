namespace IndexThinking.Client;

/// <summary>
/// Thrown when the input token count exceeds the configured context window limit.
/// </summary>
public class ContextWindowExceededException : InvalidOperationException
{
    /// <summary>The estimated input token count.</summary>
    public int InputTokens { get; }

    /// <summary>The configured maximum context tokens.</summary>
    public int MaxContextTokens { get; }

    public ContextWindowExceededException(int inputTokens, int maxContextTokens)
        : base($"Input tokens ({inputTokens}) exceed context window ({maxContextTokens}).")
    {
        InputTokens = inputTokens;
        MaxContextTokens = maxContextTokens;
    }
}
