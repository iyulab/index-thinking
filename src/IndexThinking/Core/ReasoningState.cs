namespace IndexThinking.Core;

/// <summary>
/// Provider-specific state to preserve across turns.
/// Contains opaque data like encrypted_content (OpenAI) or signatures (Anthropic/Gemini).
/// </summary>
public sealed record ReasoningState
{
    /// <summary>
    /// Provider family (openai, anthropic, gemini, etc.).
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// Opaque state data (encrypted_content, signatures, etc.).
    /// </summary>
    public required byte[] Data { get; init; }

    /// <summary>
    /// When this state was captured.
    /// </summary>
    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;
}
