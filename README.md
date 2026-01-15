# IndexThinking

> **Zero-config middleware that handles LLM response truncation, token budgeting, and reasoning extraction - so you don't have to.**

[![NuGet](https://img.shields.io/nuget/v/IndexThinking.svg)](https://www.nuget.org/packages/IndexThinking)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/)

## The Problem

Every LLM application deals with the same tedious issues:

- **Response Truncation**: Output hits token limit → you manually implement "continue" logic
- **Provider Differences**: OpenAI uses `reasoning`, Anthropic uses `thinking` blocks, Gemini has `thoughtSignature` → you write parsing code for each
- **Token Tracking**: Need to track costs and usage → you manually aggregate across requests
- **State Management**: Multi-turn reasoning requires state preservation → you build session handling

This is 50+ lines of boilerplate that every LLM app reimplements.

## The Solution

```csharp
// Before: 50+ lines of truncation handling, provider-specific parsing, token tracking...
// After: 1 line
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

## Supported Providers

IndexThinking works with any `IChatClient` and includes specialized parsers for:

| Provider | Reasoning Format | State Preservation |
|----------|-----------------|-------------------|
| **OpenAI** (o1/o3) | `reasoning` field, `encrypted_content` | ✅ |
| **Anthropic** (Claude) | `thinking` blocks with signatures | ✅ |
| **Gemini** | `thoughtSignature` | ✅ |
| **DeepSeek/Qwen** | `<think>` tags, `reasoning_content` | ✅ |
| **vLLM/Ollama** | Configurable think tags | ✅ |

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
