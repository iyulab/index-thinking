# Memory Integration Guide

This guide explains how to integrate IndexThinking with long-term memory systems like [Memory-Indexer](https://github.com/iyulab/memory-indexer) or custom memory backends.

## Overview

IndexThinking is a **Working Memory Manager** that handles the current flow of thought within a conversation turn. For persistent, cross-session knowledge, it integrates with external **Long-term Memory** systems.

```
┌─────────────────────────────────────────────────────────────┐
│                    Your Application                         │
├─────────────────────────────────────────────────────────────┤
│  IndexThinking (Working Memory)                             │
│  - Token budget management                                  │
│  - Continuation handling                                    │
│  - Response orchestration                                   │
│                    │                                        │
│                    ▼                                        │
│  IMemoryProvider (Abstraction Layer)                        │
│  - RecallAsync() - Retrieve relevant memories               │
│  - RememberAsync() - Store new memories                     │
├─────────────────────────────────────────────────────────────┤
│  Memory Backend (Long-term Memory)                          │
│  Options: Memory-Indexer, Custom Implementation, etc.       │
└─────────────────────────────────────────────────────────────┘
```

## Memory Scopes

IndexThinking supports three memory scopes:

| Scope | Description | Example |
|-------|-------------|---------|
| `User` | Cross-session facts about the user | "Prefers dark mode", "Uses TypeScript" |
| `Session` | Current session context | "Working on authentication feature" |
| `Topic` | Current topic within session | "Discussing the login component" |

## Integration Options

### Option 1: Null Provider (Default, Zero-Config)

If no memory backend is configured, IndexThinking uses `NullMemoryProvider` which silently returns empty results. This allows the library to work without any memory dependencies.

```csharp
// No memory configuration needed
var services = new ServiceCollection()
    .AddIndexThinkingAgents()
    .AddIndexThinkingInMemoryStorage()
    .BuildServiceProvider();

var client = new ChatClientBuilder(innerClient)
    .UseIndexThinking()
    .Build(services);
```

### Option 2: Function-Based Integration (Recommended)

Use delegates to integrate with any memory backend without direct package dependencies:

```csharp
services.AddIndexThinkingMemory(
    recallDelegate: async (userId, sessionId, query, limit, ct) =>
    {
        var context = await memoryService.RecallAsync(userId, sessionId, query, limit, ct);
        return new MemoryRecallResult
        {
            UserMemories = context.UserMemories
                .Select(m => (m.Content, (float?)m.ImportanceScore))
                .ToList(),
            SessionMemories = context.SessionMemories
                .Select(m => (m.Content, (float?)m.ImportanceScore))
                .ToList(),
            TopicMemories = context.TopicMemories
                .Select(m => (m.Content, (float?)m.ImportanceScore))
                .ToList()
        };
    },
    rememberDelegate: async (userId, sessionId, memories, ct) =>
    {
        foreach (var memory in memories)
        {
            await memoryService.RememberAsync(userId, sessionId, memory.Content, ct);
        }
    });
```

### Option 3: Custom Provider Implementation

Implement `IMemoryProvider` for full control:

```csharp
public class MyMemoryProvider : IMemoryProvider
{
    private readonly IMyMemoryBackend _backend;

    public bool IsConfigured => true;

    public async Task<MemoryRecallContext> RecallAsync(
        string userId,
        string? sessionId,
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var results = await _backend.SearchAsync(userId, sessionId, query, limit, cancellationToken);

        return new MemoryRecallContext
        {
            Query = query,
            Memories = results.Select(r => new MemoryEntry
            {
                Content = r.Content,
                Scope = r.Scope,
                Relevance = r.Score
            }).ToList(),
            UserMemories = results.Where(r => r.Scope == "user").Select(...).ToList(),
            SessionMemories = results.Where(r => r.Scope == "session").Select(...).ToList(),
            TopicMemories = results.Where(r => r.Scope == "topic").Select(...).ToList()
        };
    }

    public async Task RememberAsync(
        string userId,
        string? sessionId,
        IEnumerable<MemoryStoreRequest> memories,
        CancellationToken cancellationToken = default)
    {
        foreach (var memory in memories)
        {
            await _backend.StoreAsync(
                userId,
                sessionId,
                memory.Content,
                memory.Scope.ToString(),
                memory.Tags,
                memory.Metadata,
                cancellationToken);
        }
    }
}

// Registration
services.AddIndexThinkingMemory<MyMemoryProvider>();
```

---

## Memory-Indexer Integration

This section provides detailed integration patterns with [Memory-Indexer](https://github.com/iyulab/memory-indexer).

### Basic Integration

```csharp
// Program.cs
var builder = Host.CreateApplicationBuilder(args);

// 1. Register Memory-Indexer
builder.Services.AddMemoryIndexer(options =>
{
    options.ConnectionString = "Data Source=memories.db";
});

// 2. Register IndexThinking
builder.Services.AddIndexThinkingAgents();
builder.Services.AddIndexThinkingInMemoryStorage();

// 3. Wire up Memory-Indexer as IMemoryProvider
builder.Services.AddIndexThinkingMemory(
    recallDelegate: async (userId, sessionId, query, limit, ct) =>
    {
        var sp = builder.Services.BuildServiceProvider();
        var memoryService = sp.GetRequiredService<IMemoryService>();
        var context = await memoryService.RecallAsync(userId, sessionId, query, limit, ct);

        return new MemoryRecallResult
        {
            UserMemories = context.UserMemories
                .Select(m => (m.Content, (float?)m.ImportanceScore))
                .ToList(),
            SessionMemories = context.SessionMemories
                .Select(m => (m.Content, (float?)m.ImportanceScore))
                .ToList(),
            TopicMemories = context.TopicMemories
                .Select(m => (m.Content, (float?)m.ImportanceScore))
                .ToList()
        };
    },
    rememberDelegate: async (userId, sessionId, memories, ct) =>
    {
        var sp = builder.Services.BuildServiceProvider();
        var memoryService = sp.GetRequiredService<IMemoryService>();

        foreach (var memory in memories)
        {
            await memoryService.RememberAsync(userId, sessionId, memory.Content, ct);
        }
    });
```

### Full Integration with Extended Metadata

For richer integration that preserves Memory-Indexer's metadata (importance, stability, type, etc.):

```csharp
builder.Services.AddSingleton<IMemoryProvider>(sp =>
{
    var memoryService = sp.GetRequiredService<IMemoryService>();

    return new FuncMemoryProvider(
        recallDelegate: async (userId, sessionId, query, limit, ct) =>
        {
            var context = await memoryService.RecallAsync(userId, sessionId, query, limit, ct);

            // Convert with full metadata
            static MemoryEntry ToEntry(MemoryUnit m) => new()
            {
                Content = m.Content,
                Scope = m.Scope.ToString().ToLower(),
                Relevance = m.ImportanceScore,
                StoredAt = new DateTimeOffset(m.CreatedAt, TimeSpan.Zero),
                // Extended properties from Memory-Indexer
                Id = m.Id,
                ImportanceScore = m.ImportanceScore,
                MemoryType = m.Type.ToString(),
                Stability = m.Stability.ToString(),
                RetentionScore = m.RetentionScore,
                Topics = m.Topics,
                Entities = m.Entities,
                Role = m.Role,
                AccessCount = m.AccessCount
            };

            return new MemoryRecallResult
            {
                UserMemories = context.UserMemories
                    .Select(m => (m.Content, (float?)m.ImportanceScore))
                    .ToList(),
                SessionMemories = context.SessionMemories
                    .Select(m => (m.Content, (float?)m.ImportanceScore))
                    .ToList(),
                TopicMemories = context.TopicMemories
                    .Select(m => (m.Content, (float?)m.ImportanceScore))
                    .ToList()
            };
        },
        rememberDelegate: async (userId, sessionId, memories, ct) =>
        {
            foreach (var memory in memories)
            {
                // Memory-Indexer auto-detects type and handles scope
                await memoryService.RememberAsync(
                    userId,
                    sessionId ?? string.Empty,
                    memory.Content,
                    cancellationToken: ct);
            }
        });
});
```

### Session Lifecycle Management

Memory-Indexer provides session lifecycle methods. Call these directly from your application:

```csharp
public class ChatService
{
    private readonly IChatClient _chatClient;
    private readonly IMemoryService _memoryService;

    public async Task EndConversationAsync(string userId, string sessionId)
    {
        // Memory-Indexer consolidates session memories
        // - Promotes worthy memories to User scope
        // - Archives session context
        await _memoryService.EndSessionAsync(userId, sessionId);
    }

    public async Task DeleteUserDataAsync(string userId)
    {
        // GDPR "right to be forgotten"
        await _memoryService.ForgetUserAsync(userId);
    }

    public async Task ClearSessionAsync(string userId, string sessionId)
    {
        // Clear session without affecting User memories
        await _memoryService.ForgetSessionAsync(userId, sessionId);
    }
}
```

### Type Mapping Reference

| Memory-Indexer | IndexThinking | Description |
|----------------|---------------|-------------|
| `MemoryUnit.Content` | `MemoryEntry.Content` | Memory content |
| `MemoryUnit.Scope` | `MemoryEntry.Scope` | User/Session/Topic |
| `MemoryUnit.ImportanceScore` | `MemoryEntry.ImportanceScore` | LLM-rated importance (0-1) |
| `MemoryUnit.Type` | `MemoryEntry.MemoryType` | Episodic/Semantic/Procedural/Fact |
| `MemoryUnit.Stability` | `MemoryEntry.Stability` | Volatile/Stabilizing/Stable/Consolidated |
| `MemoryUnit.RetentionScore` | `MemoryEntry.RetentionScore` | Ebbinghaus retention (0-1) |
| `MemoryUnit.Topics` | `MemoryEntry.Topics` | Extracted topic labels |
| `MemoryUnit.Entities` | `MemoryEntry.Entities` | Extracted named entities |
| `MemoryUnit.Role` | `MemoryEntry.Role` | user/assistant/system |
| `MemoryUnit.AccessCount` | `MemoryEntry.AccessCount` | Retrieval frequency |

---

## API Reference

### MemoryEntry

Core and extended properties:

```csharp
public sealed record MemoryEntry
{
    // Core properties (always available)
    public required string Content { get; init; }
    public string? Scope { get; init; }           // "user", "session", "topic"
    public float? Relevance { get; init; }         // 0.0 to 1.0
    public DateTimeOffset? StoredAt { get; init; }

    // Extended properties (Memory-Indexer compatible)
    public Guid? Id { get; init; }
    public float? ImportanceScore { get; init; }   // LLM-rated importance
    public string? MemoryType { get; init; }       // Episodic, Semantic, etc.
    public string? Stability { get; init; }        // Volatile, Stable, etc.
    public float? RetentionScore { get; init; }    // Ebbinghaus curve
    public IReadOnlyList<string>? Topics { get; init; }
    public IReadOnlyList<string>? Entities { get; init; }
    public string? Role { get; init; }             // user, assistant, system
    public int? AccessCount { get; init; }
}
```

### MemoryStoreRequest

Request object for storing memories:

```csharp
var request = new MemoryStoreRequest
{
    Content = "User prefers dark mode",       // Required
    Scope = MemoryScope.User,                 // Default: Session
    TopicId = "ui-preferences",               // For Topic scope
    Tags = ["preference", "ui"],              // Categorization
    Metadata = new Dictionary<string, string>
    {
        ["source"] = "settings-page"
    }
};
```

### MemoryRecallResult

Delegate return type for recall operations:

```csharp
public sealed record MemoryRecallResult
{
    public IReadOnlyList<(string Content, float? Relevance)> UserMemories { get; init; }
    public IReadOnlyList<(string Content, float? Relevance)> SessionMemories { get; init; }
    public IReadOnlyList<(string Content, float? Relevance)> TopicMemories { get; init; }
}
```

### MemoryScope Enum

```csharp
public enum MemoryScope
{
    User = 0,     // Cross-session, persists indefinitely
    Session = 1,  // Current session only
    Topic = 2     // Current topic within session
}
```

---

## Best Practices

1. **Use Session Scope for Current Context**: Session-scoped memories automatically expire with the session.

2. **Use User Scope Sparingly**: Only store truly persistent facts that apply across all sessions.

3. **Include Relevance Scores**: When implementing recall, include relevance scores to help IndexThinking prioritize memories.

4. **Handle Failures Gracefully**: If memory backend fails, IndexThinking should continue working (degraded but functional).

5. **Consider Privacy**: Memory content may contain sensitive information. Implement appropriate data handling policies.

6. **Call EndSessionAsync**: When a conversation ends, call Memory-Indexer's `EndSessionAsync` to consolidate memories.

---

## Troubleshooting

### Memories Not Being Recalled

1. Check `IMemoryProvider.IsConfigured` returns `true`
2. Verify the recall delegate is returning results
3. Check query relevance - memories must be semantically related to the query

### Memories Not Being Stored

1. Ensure `rememberDelegate` is provided to `AddIndexThinkingMemory`
2. Check for exceptions in the remember delegate
3. Verify the memory backend is accepting writes

### Performance Issues

1. Limit recall results (default: 10) to reduce context size
2. Use appropriate memory scopes to reduce search space
3. Consider caching frequently accessed memories
