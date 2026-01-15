namespace IndexThinking.Memory;

/// <summary>
/// Represents a request to store a memory in a long-term memory system.
/// </summary>
/// <remarks>
/// <para>
/// This record provides a simple contract for memory storage requests
/// without requiring specific backend types directly.
/// </para>
/// <para>
/// Example usage:
/// </para>
/// <code>
/// var request = new MemoryStoreRequest
/// {
///     Content = "User prefers dark mode",
///     Scope = MemoryScope.User,
///     Tags = ["preference", "ui"]
/// };
/// await memoryProvider.RememberAsync(userId, sessionId, [request], ct);
/// </code>
/// </remarks>
public sealed record MemoryStoreRequest
{
    /// <summary>
    /// The content to store as memory.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// The scope of the memory.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="MemoryScope.Session"/>.
    /// </remarks>
    public MemoryScope Scope { get; init; } = MemoryScope.Session;

    /// <summary>
    /// Optional topic identifier for topic-scoped memories.
    /// </summary>
    /// <remarks>
    /// Only relevant when <see cref="Scope"/> is <see cref="MemoryScope.Topic"/>.
    /// </remarks>
    public string? TopicId { get; init; }

    /// <summary>
    /// Optional tags for categorizing the memory.
    /// </summary>
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>
    /// Optional metadata for the memory.
    /// </summary>
    /// <remarks>
    /// Use this for additional context that may help with recall or organization.
    /// Keys and values are application-specific.
    /// </remarks>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
