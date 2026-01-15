using IndexThinking.Continuation;
using IndexThinking.Core;
using IndexThinking.Memory;
using Microsoft.Extensions.AI;

namespace IndexThinking.Agents;

/// <summary>
/// Contextual information for a single thinking turn.
/// Passed between internal components during turn processing.
/// </summary>
public sealed record ThinkingContext
{
    /// <summary>
    /// Unique identifier for this turn.
    /// </summary>
    public required string TurnId { get; init; }

    /// <summary>
    /// Session identifier for state correlation.
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// User identifier for memory operations (optional).
    /// When provided, enables memory recall for this turn.
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// The original user request messages.
    /// </summary>
    public required IList<ChatMessage> Messages { get; init; }

    /// <summary>
    /// Memory context recalled for this turn (if memory is enabled).
    /// Contains relevant memories from user, session, and topic scopes.
    /// </summary>
    public MemoryRecallContext? MemoryContext { get; init; }

    /// <summary>
    /// Budget configuration for this turn.
    /// </summary>
    public BudgetConfig Budget { get; init; } = new();

    /// <summary>
    /// Continuation configuration for handling truncated responses.
    /// </summary>
    public ContinuationConfig Continuation { get; init; } = ContinuationConfig.Default;

    /// <summary>
    /// Estimated task complexity (if pre-computed).
    /// </summary>
    public TaskComplexity? EstimatedComplexity { get; init; }

    /// <summary>
    /// Model identifier (if known).
    /// </summary>
    public string? ModelId { get; init; }

    /// <summary>
    /// When this turn started.
    /// </summary>
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Cancellation token for this turn.
    /// </summary>
    public CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// Creates a new context with a generated turn ID.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="messages">The user request messages.</param>
    /// <returns>A new thinking context.</returns>
    public static ThinkingContext Create(string sessionId, IList<ChatMessage> messages)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(messages);

        return new ThinkingContext
        {
            TurnId = Guid.NewGuid().ToString("N"),
            SessionId = sessionId,
            Messages = messages
        };
    }

    /// <summary>
    /// Creates a copy with the specified complexity.
    /// </summary>
    public ThinkingContext WithComplexity(TaskComplexity complexity) =>
        this with { EstimatedComplexity = complexity };

    /// <summary>
    /// Creates a copy with the specified model ID.
    /// </summary>
    public ThinkingContext WithModel(string modelId) =>
        this with { ModelId = modelId };

    /// <summary>
    /// Creates a copy with the specified budget configuration.
    /// </summary>
    public ThinkingContext WithBudget(BudgetConfig budget) =>
        this with { Budget = budget };

    /// <summary>
    /// Creates a copy with the specified cancellation token.
    /// </summary>
    public ThinkingContext WithCancellation(CancellationToken cancellationToken) =>
        this with { CancellationToken = cancellationToken };

    /// <summary>
    /// Creates a copy with the specified user ID.
    /// </summary>
    public ThinkingContext WithUserId(string userId) =>
        this with { UserId = userId };

    /// <summary>
    /// Creates a copy with the specified memory context.
    /// </summary>
    public ThinkingContext WithMemory(MemoryRecallContext memoryContext) =>
        this with { MemoryContext = memoryContext };

    /// <summary>
    /// Whether memory is available for this context.
    /// </summary>
    public bool HasMemory => MemoryContext is not null && MemoryContext.HasMemories;
}
