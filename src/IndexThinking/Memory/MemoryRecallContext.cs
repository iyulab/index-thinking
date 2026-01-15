namespace IndexThinking.Memory;

/// <summary>
/// Represents a single memory entry recalled from a memory provider.
/// </summary>
/// <remarks>
/// <para>
/// Core properties (Content, Scope, Relevance, StoredAt) are always available.
/// Extended properties (ImportanceScore, MemoryType, etc.) are populated when
/// using rich memory backends like Memory-Indexer.
/// </para>
/// </remarks>
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

    // ========================================
    // Extended Properties (Memory-Indexer compatible)
    // ========================================

    /// <summary>
    /// Unique identifier for this memory entry.
    /// </summary>
    /// <remarks>
    /// Populated by rich memory backends for tracking and updates.
    /// </remarks>
    public Guid? Id { get; init; }

    /// <summary>
    /// LLM-assigned importance score (0.0 to 1.0).
    /// </summary>
    /// <remarks>
    /// Based on Memory-Indexer's poignancy rating system.
    /// Higher values indicate more significant memories.
    /// </remarks>
    public float? ImportanceScore { get; init; }

    /// <summary>
    /// The type of memory (e.g., "Episodic", "Semantic", "Procedural", "Fact").
    /// </summary>
    /// <remarks>
    /// Based on Tulving's memory classification.
    /// Populated by Memory-Indexer when using type classification.
    /// </remarks>
    public string? MemoryType { get; init; }

    /// <summary>
    /// Memory stability level (e.g., "Volatile", "Stabilizing", "Stable", "Consolidated", "Permanent").
    /// </summary>
    /// <remarks>
    /// Based on Ebbinghaus forgetting curve model.
    /// Higher stability = longer retention without reinforcement.
    /// </remarks>
    public string? Stability { get; init; }

    /// <summary>
    /// Current retention score based on forgetting curve (0.0 to 1.0).
    /// </summary>
    /// <remarks>
    /// Calculated using Ebbinghaus formula: R = e^(-t/S).
    /// Lower values indicate memories that may be forgotten soon.
    /// </remarks>
    public float? RetentionScore { get; init; }

    /// <summary>
    /// Topic labels extracted from the content.
    /// </summary>
    public IReadOnlyList<string>? Topics { get; init; }

    /// <summary>
    /// Named entities extracted from the content.
    /// </summary>
    public IReadOnlyList<string>? Entities { get; init; }

    /// <summary>
    /// Role of the original message sender (user, assistant, system).
    /// </summary>
    /// <remarks>
    /// Preserved for episodic memories, typically null for semantic memories.
    /// </remarks>
    public string? Role { get; init; }

    /// <summary>
    /// Number of times this memory has been accessed.
    /// </summary>
    public int? AccessCount { get; init; }
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
