namespace IndexThinking.Memory;

/// <summary>
/// Represents a single memory entry recalled from a memory provider.
/// </summary>
public sealed record MemoryEntry
{
    /// <summary>
    /// The content of the memory.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// The scope of the memory (e.g., "user", "session", "topic").
    /// </summary>
    public string? Scope { get; init; }

    /// <summary>
    /// Relevance score of the memory to the query (0.0 to 1.0).
    /// </summary>
    public float? Relevance { get; init; }

    /// <summary>
    /// When the memory was originally stored.
    /// </summary>
    public DateTimeOffset? StoredAt { get; init; }
}

/// <summary>
/// Result of a memory recall operation.
/// Contains memories grouped by scope and metadata about the recall.
/// </summary>
public sealed record MemoryRecallContext
{
    /// <summary>
    /// The query used to recall memories.
    /// </summary>
    public required string Query { get; init; }

    /// <summary>
    /// All recalled memories.
    /// </summary>
    public IReadOnlyList<MemoryEntry> Memories { get; init; } = [];

    /// <summary>
    /// User-scoped memories (cross-session facts).
    /// </summary>
    public IReadOnlyList<MemoryEntry> UserMemories { get; init; } = [];

    /// <summary>
    /// Session-scoped memories (current session context).
    /// </summary>
    public IReadOnlyList<MemoryEntry> SessionMemories { get; init; } = [];

    /// <summary>
    /// Topic-scoped memories (current topic within session).
    /// </summary>
    public IReadOnlyList<MemoryEntry> TopicMemories { get; init; } = [];

    /// <summary>
    /// When the recall operation was performed.
    /// </summary>
    public DateTimeOffset RecalledAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Whether any memories were recalled.
    /// </summary>
    public bool HasMemories => Memories.Count > 0;

    /// <summary>
    /// Total count of all recalled memories.
    /// </summary>
    public int TotalCount => Memories.Count;

    /// <summary>
    /// Empty recall context (no memories found).
    /// </summary>
    public static MemoryRecallContext Empty(string query) => new()
    {
        Query = query,
        Memories = [],
        UserMemories = [],
        SessionMemories = [],
        TopicMemories = []
    };
}
