# IndexThinking

> **Build better chat apps, not infrastructure.**
> Zero-config SDK that handles the repetitive-but-hard parts of LLM integration: query preprocessing, response continuation, follow-up suggestions, and more.

[![NuGet](https://img.shields.io/nuget/v/IndexThinking.svg)](https://www.nuget.org/packages/IndexThinking)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/)

## Design Philosophy

- **Zero-configuration by default** - Works out of the box, does the right thing automatically
- **Extensible for power users** - Every component is configurable and replaceable
- **Solves hard problems** - Handles tasks that are repetitive but difficult to do well

## The Problem

LLM APIs are stateless. Every chat app reimplements the same infrastructure:

```
LLM API expects:                    But users send:
──────────────────                  ─────────────────
{                                   "Do that thing again"
  "messages": [                     "Make it faster"
    // YOU manage history           "Fix that file"
    // YOU handle summarization
    // YOU interpret context        → Ambiguous, contextual,
    // YOU detect truncation          requires session state
    // YOU parse reasoning
  ]
}
```

**Every chat app developer writes:**
- Session/history management (sliding window, summarization)
- Context interpretation ("that thing from before" → actual meaning)
- Truncation handling (continue logic, fragment merging)
- Provider-specific parsing (5+ different reasoning formats)
- Token budget tracking
- Follow-up question suggestions

These are **repetitive but hard to do well**. IndexThinking handles them for you.

## The Solution

### Vision: Session-Aware API (Coming in v0.9.0)

```csharp
// What you send:
var response = await thinking.ChatAsync(
    userId: "user-123",
    sessionId: "session-456",
    message: "Do that thing again"
);

// What IndexThinking does internally:
// 1. Load session history
// 2. Interpret "that thing" → "save DataFrame to CSV"
// 3. Build optimized prompt with context
// 4. Send to LLM, handle truncation
// 5. Parse reasoning, track tokens
// 6. Save updated session state
```

### Current: Middleware API (Available Now)

```csharp
var client = new ChatClientBuilder(innerClient)
    .UseIndexThinking()
    .Build();

var response = await client.GetResponseAsync(messages);
// Truncation? Auto-continued.
// Thinking content? Auto-extracted.
// Token usage? Auto-tracked.
```

## Quick Start

```bash
dotnet add package IndexThinking
```

### Basic Usage (Zero-Config)

```csharp
using IndexThinking.Client;
using IndexThinking.Extensions;
using Microsoft.Extensions.AI;

// Register services
services.AddIndexThinkingAgents();

// Wrap any IChatClient
var client = new ChatClientBuilder(openAIClient)
    .UseIndexThinking()
    .Build();

// Use normally - IndexThinking handles the rest
var response = await client.GetResponseAsync(messages);

// Access extracted metadata when needed
var thinking = response.GetThinkingContent();   // Parsed reasoning
var metrics = response.GetTurnMetrics();        // Token usage
var result = response.GetTurnResult();          // Full turn info
```

### Customization (When Needed)

```csharp
.UseIndexThinking(options =>
{
    // Token budgets
    options.DefaultBudget = new BudgetConfig
    {
        ThinkingBudget = 8192,
        AnswerBudget = 4096,
        MaxContinuations = 10
    };

    // Continuation behavior
    options.DefaultContinuation = new ContinuationConfig
    {
        MaxContinuations = 5,
        MinimumProgress = 100
    };

    // Complexity estimation
    options.AutoEstimateComplexity = true;
})
```

## What It Handles

| Feature | Without IndexThinking | With IndexThinking |
|---------|----------------------|-------------------|
| **Truncation Recovery** | Manual continuation loop, fragment merging | Automatic |
| **Reasoning Extraction** | Provider-specific parsing (5+ formats) | Unified API |
| **Token Budgeting** | Manual counting per provider | Auto-tracked |
| **State Continuity** | Custom session management | Built-in |
| **JSON/Code Recovery** | Manual truncated content repair | Automatic |
| **Context Interpretation** | Manual "that thing" resolution | Coming v0.8.5 |
| **Follow-up Suggestions** | Manual question generation | Coming v0.8.5 |
| **Session Management** | Manual history, summarization | Coming v0.9.0 |

## Supported Providers

IndexThinking works with any `IChatClient` and includes specialized parsers for:

| Provider | Reasoning Format | Truncation Handling | State Preservation |
|----------|-----------------|--------------------|--------------------|
| **OpenAI** (o1/o3) | `reasoning` field, `encrypted_content` | `length`, `content_filter` | ✅ |
| **Anthropic** (Claude) | `thinking` blocks with signatures | `max_tokens`, `refusal`, `context_window_exceeded` | ✅ |
| **Google Gemini** | `thoughtSignature` | `MAX_TOKENS`, `SAFETY`, `RECITATION` | ✅ |
| **DeepSeek/Qwen** | `<think>` tags, `reasoning_content` | OpenAI-compatible | ✅ |
| **vLLM/GPUStack/Ollama** | Configurable think tags | `length` (OpenAI-compatible) | ✅ |

### Truncation Reasons

IndexThinking detects and handles various provider-specific stop/finish reasons:

| Reason | Description | Providers |
|--------|-------------|-----------|
| `TokenLimit` | Max output tokens reached | All (length, max_tokens, MAX_TOKENS) |
| `ContentFiltered` | Safety/content filter triggered | OpenAI, Google |
| `Recitation` | Copyright/recitation concern | Google |
| `Refusal` | Model safety refusal | Anthropic |
| `ContextWindowExceeded` | Context window limit exceeded | Anthropic |
| `UnbalancedStructure` | Incomplete braces/brackets | Structural detection |
| `IncompleteCodeBlock` | Unclosed code blocks | Structural detection |
| `MidSentence` | Response ends mid-sentence | Heuristic detection |

## IndexThinking vs Agent Orchestration

**IndexThinking is NOT an orchestrator.** This distinction is fundamental:

| Aspect | IndexThinking | Agent Orchestrators |
|--------|---------------|---------------------|
| **Scope** | Single LLM turn | Multi-step workflows |
| **Role** | Building block | Workflow coordinator |
| **Examples** | - | LangChain, AutoGen, Semantic Kernel |
| **Relationship** | Used BY orchestrators | USES IndexThinking |

```
┌─────────────────────────────────────────────────┐
│           Agent Orchestrator                    │
│  (AutoGen, Semantic Kernel, LangGraph, etc.)    │
├─────────────────────────────────────────────────┤
│  Task 1          Task 2          Task 3         │
│    ↓                ↓                ↓          │
│ ┌──────────┐  ┌──────────┐  ┌──────────┐       │
│ │IChatClient│  │IChatClient│  │IChatClient│     │
│ │  + Index  │  │  + Index  │  │  + Index  │     │
│ │ Thinking  │  │ Thinking  │  │ Thinking  │     │
│ └──────────┘  └──────────┘  └──────────┘       │
└─────────────────────────────────────────────────┘
```

IndexThinking optimizes each individual LLM call. Orchestrators coordinate multiple calls.

## IndexThinking vs Memory-Indexer

| Feature | **Memory-Indexer** | **IndexThinking** |
|---------|-------------------|-------------------|
| **Perspective** | Long-term Memory (Past) | Working Memory (Present) |
| **Data Focus** | Past conversations, Knowledge Base | Current thought process, Turn state |
| **System Role** | Librarian (Search & Retrieve) | Architect (Plan & Execute) |
| **Primary Goal** | "What do we know?" | "Where are we in this task?" |

They're complementary: Memory-Indexer recalls past knowledge, IndexThinking manages current reasoning flow.

## Architecture

IndexThinking follows `Microsoft.Extensions.AI` patterns:

```csharp
// Decorator pattern - wraps any IChatClient
public class ThinkingChatClient : DelegatingChatClient
{
    // Intercepts GetResponseAsync
    // Handles truncation, parsing, tracking
    // Returns enriched response
}
```

Core components:
- **ThinkingChatClient**: Main integration point (DelegatingChatClient)
- **IThinkingTurnManager**: Orchestrates single-turn processing
- **IReasoningParser**: Provider-specific content extraction
- **ITruncationDetector**: Detects incomplete responses
- **ITokenCounter**: Multi-provider token counting

## Documentation

- [Roadmap](docs/ROADMAP.md) - Development phases and architecture decisions
- [API Reference](docs/API.md) - Coming soon

## License

MIT License - See [LICENSE](LICENSE) for details.
