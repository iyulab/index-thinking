# IndexThinking

> **Working Memory Manager for Reasoning-capable LLMs**

[![NuGet](https://img.shields.io/nuget/v/IndexThinking.svg)](https://www.nuget.org/packages/IndexThinking)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/)

## What It Does

IndexThinking handles the repetitive-but-hard parts of LLM integration:

- **Truncation Recovery** - Auto-continue when responses hit token limits (`GetResponseAsync`; a truncated stream is reported as `WasTruncated`)
- **Reasoning Extraction** - Unified API for provider-specific thinking formats
- **Context Tracking** - Session-aware conversation with sliding window
- **Token Management** - Token usage tracking (`IBudgetTracker`) and complexity estimation (`IComplexityEstimator`), reported in turn metrics — informational, not limits. To cap how much a model thinks, set it on the request: `ChatOptions.Reasoning` (effort) and `ChatOptions.MaxOutputTokens`; IndexThinking extracts the reasoning, it does not throttle it
- **Content Recovery** - Repair truncated JSON/code blocks
- **Thinking State Stores** - `IThinkingStateStore` implementations: in-memory (`AddIndexThinkingInMemoryStorage`) and distributed-cache (`AddIndexThinkingDistributedStorage`) in the core package, persistent SQLite in `IndexThinking.Sqlite`. All are opt-in — `AddIndexThinkingAgents` registers no store, and without one no per-session state is saved

## Features

Each row: what it does · entry point · how to turn it on. "Default" means the Quick Start registration (`AddIndexThinkingAgents()` + `UseIndexThinking()`) already enables it.

| Feature | Entry point | How to turn it on |
|---|---|---|
| Turn pipeline over any `IChatClient` | `ChatClientBuilder.UseIndexThinking()` → `ThinkingChatClient` | Register `AddIndexThinkingAgents()` (required — resolves `IThinkingTurnManager`), then `UseIndexThinking(o => …)`; or `new ThinkingChatClient(inner, turnManager, options, …)` without DI |
| Truncation detection + auto-continuation | `ITruncationDetector`, `IContinuationHandler`; limits in `ThinkingChatClientOptions.DefaultContinuation` (`ContinuationConfig`: `MaxContinuations` 5, `MaxTotalDuration` 5 min, …) | Default on `GetResponseAsync`. A truncated stream is not continued — it is reported as `TurnResult.WasTruncated` |
| Truncated JSON / code-block repair | `ContentRecoveryUtils` (`TryRecoverJson`, `TryRecoverCodeBlocks`, `CombineFragments`) | Default during continuation (`ContinuationConfig.EnableJsonRecovery` / `EnableCodeBlockRecovery`, both `true`); also callable directly |
| Reasoning extraction (OpenAI, Anthropic, Gemini, `<think>` tags) | `IReasoningParser`, `ReasoningParserRegistry.Default`; result via `response.GetThinkingContent()` | Default (`IncludeThinkingInMetadata = true`) |
| Turn metrics | `response.GetTurnMetrics()` / `GetTurnResult()`; on a stream, the final update's `AdditionalProperties[ThinkingChatClient.TurnResultKey]` | Default (`IncludeMetricsInMetadata = true`) |
| Reasoning activation for DeepSeek / Qwen / vLLM / GPUStack | `ReasoningRequestModifierRegistry.Default` | Opt-in: `ThinkingChatClientOptions.EnableReasoning = true` (`AutoDetectReasoningRequirement` keeps it to models that need it). A custom registry from `AddIndexThinkingReasoningModifiers(…)` is what the DI `UseIndexThinking()` uses (or pass `modifierRegistry:` to the `ThinkingChatClient` constructor); registering it does not turn reasoning on by itself |
| Live `<think>` separation in streams | `ThinkingChatClientOptions.SeparateReasoningInStream` | Opt-in (default `false`) — see below |
| Session context tracking + injection | `IContextTracker` (`InMemoryContextTracker`), `IContextInjector`; session id from `client.ChatAsync(sessionId, …)` / `SendAsync` or `ChatOptions` key `IndexThinking.SessionId` | Opt-in: `AddIndexThinkingContext(…)` (`ContextTrackerOptions.MaxTurns` 10, `SessionTtl` 1 h). `EnableContextTracking` / `EnableContextInjection` turn either half off |
| Fit a request into a token budget | `ContextBudgetChatReducer` (`ContextBudgetOptions`: `MaxInputTokens` required, `ToolResults` = `ToolResultTrimmer` — 30 000 characters, first 50 and last 20 lines) — an M.E.AI `IChatReducer`: tool results over the limit keep their first and last lines, then the oldest messages leave, a tool round (call + results) always whole; system messages, the last user message and the newest round stay; a note marks each gap | Opt-in: `ChatClientBuilder.UseChatReducer(new ContextBudgetChatReducer(new() { MaxInputTokens = … }))`, registered after `UseFunctionInvocation()` so every round of a tool loop is fitted |
| Token counting | `ITokenCounter` / `IChatMessageTokenCounter`: `ApproximateTokenCounter` (registered by `AddIndexThinkingAgents`), `TiktokenTokenCounter.ForModel(…)`, `TokenCounterFactory` | Default approximate counter — within a few percent of o200k_base on prose, Korean, code and JSON; a message counts its text, tool calls and results, reasoning and images (`ImageTokens` each). Other tokenizers differ (cl100k needs ~1.5x more for Korean). The client also counts the request's tool definitions and, by default, scales its estimates by what the provider reports (`CalibrateTokenCounts`, `TokenEstimateFactor`). Register your own `ITokenCounter` before `AddIndexThinkingAgents()` to replace it |
| Output-token boost and cap to the context window | `ThinkingChatClientOptions.ThinkingOutputMultiplier` (2, applied when reasoning is enabled), `MaxContextTokens` | Default on `GetResponseAsync` when the request sets `MaxOutputTokens` and a token counter is available (the cap); not applied to streams |
| Budget / complexity reporting | `IBudgetTracker`, `IComplexityEstimator` | Default; informational only (see What It Does) |
| Per-session thinking state | `IThinkingStateStore` | Opt-in: `AddIndexThinkingInMemoryStorage()`, `AddIndexThinkingDistributedStorage()` (needs an `IDistributedCache`), or `AddIndexThinkingSqliteStorage(connectionString)` from `IndexThinking.Sqlite` |
| Store health check | `ThinkingStateStoreHealthCheck` | Opt-in: `AddIndexThinkingHealthChecks()`, then `services.AddHealthChecks().AddCheck<ThinkingStateStoreHealthCheck>("thinking-state-store")` |
| Metrics (meter `IndexThinking`: `indexthinking.turns.total`, `.continuations.total`, `.truncations.total`, `.tokens.thinking`, `indexthinking.turn.duration`) | `IndexThinkingMeter` | Opt-in: `AddIndexThinkingMetrics()` — the DI `UseIndexThinking()` records every turn on it (or pass `meter:` to the `ThinkingChatClient` constructor) |

## Scope

IndexThinking shapes **one model call** — it fits what goes in to the context window and reads what comes out — not multi-step workflows.

| IndexThinking | Agent Orchestrators |
|---------------|---------------------|
| Single turn optimization | Multi-step coordination |
| Building block | Workflow controller |
| Used BY orchestrators | Uses IndexThinking |

**Token counting role boundary** — IndexThinking owns token *counting* (`ITokenCounter`, framework-neutral; `IChatMessageTokenCounter` for M.E.AI `ChatMessage` counting). Model *metadata* (context window, pricing) belongs to TokenMeter; the consumer picks the budget from the window it knows, and `ContextBudgetChatReducer` fits a request into it.

**Long-term memory boundary** — IndexThinking keeps no long-term memory and calls no memory store. To give a turn recalled memories, recall them before the turn and add them to the messages you send.

## Packages

| Package | Use |
|---|---|
| `IndexThinking` | Turn handling, reasoning extraction, token counting, in-memory and distributed-cache state stores |
| `IndexThinking.Sqlite` | Persistent SQLite `IThinkingStateStore` — `services.AddIndexThinkingSqliteStorage("Data Source=thinking.db")`. Carries `Microsoft.Data.Sqlite` and the native `e_sqlite3`; the core package does not |

## Quick Start

```bash
dotnet add package IndexThinking
```

```csharp
// Register services
services.AddIndexThinkingAgents();
services.AddIndexThinkingContext();

// Wrap any IChatClient
var client = new ChatClientBuilder(innerClient)
    .UseIndexThinking()
    .Build(serviceProvider);

// Use normally
var response = await client.GetResponseAsync(messages);

// Access metadata
var thinking = response.GetThinkingContent();
var metrics = response.GetTurnMetrics();
```

### Session-Aware Chat

```csharp
// Context is tracked and injected per session (requires AddIndexThinkingContext)
string answer = await client.ChatAsync("session-123", "Do that again");
```

### Streaming with Thinking Orchestration

Streaming uses a **Collect-and-Yield** pattern: chunks are yielded to the caller immediately while buffered internally. After the stream completes, the buffered response is processed through the full orchestration pipeline (reasoning parsing, budget tracking, context tracking). A stream cut off at the output limit is not continued — the caller already has every chunk — and is reported as `TurnResult.WasTruncated`. The output-token boost and the context-window cap of `GetResponseAsync` do not apply either: a streamed request is sent with the caller's own `MaxOutputTokens`.

```csharp
await foreach (var update in client.GetStreamingResponseAsync(messages))
{
    // Real-time chunks arrive here
    Console.Write(update.Text);

    // The final update contains orchestration metadata
    if (update.AdditionalProperties?.ContainsKey(ThinkingChatClient.TurnResultKey) == true)
    {
        var result = update.AdditionalProperties[ThinkingChatClient.TurnResultKey] as TurnResult;
        Console.WriteLine($"\nTokens: {result?.Metrics.TotalTokens}");
    }
}
```

#### Live reasoning separation (opt-in)

Open-source / local providers (DeepSeek, Qwen3, vLLM-served) emit reasoning inline in the text delta as
`<think>…</think>` rather than as a separate channel. Enable `SeparateReasoningInStream` to have IndexThinking
split it live — answer text arrives as `TextContent`, reasoning as `TextReasoningContent` — even when a tag is
split across chunk boundaries. No bespoke tag parser needed. Default is off (raw pass-through). Native reasoning
providers (OpenAI o-series, Anthropic, Gemini) already emit a separate channel, so this is a no-op for them.

> **Migration note:** with separation enabled, reasoning spans are no longer part of `ChatResponseUpdate.Text`
> (which concatenates `TextContent` only). Consumers reading `update.Text` must switch to iterating
> `update.Contents` (as below) or reasoning deltas will be silently dropped.

```csharp
var client = new ChatClientBuilder(innerClient)
    .UseIndexThinking(o =>
    {
        o.SeparateReasoningInStream = true;         // default false
        // o.StreamingReasoningStartTag = "<think>";  // defaults shown
        // o.StreamingReasoningEndTag   = "</think>";
    })
    .Build(serviceProvider);

await foreach (var update in client.GetStreamingResponseAsync(messages))
{
    foreach (var content in update.Contents)
    {
        if (content is TextReasoningContent reasoning)
            RenderThinking(reasoning.Text);   // live "💭 thinking…" UI
        else if (content is TextContent answer)
            RenderAnswer(answer.Text);
    }
}
```

## Supported Providers

| Provider | Reasoning Format | Truncation Handling | Requires Activation |
|----------|-----------------|---------------------|---------------------|
| **OpenAI** | `reasoning` field | `length`, `content_filter` | No (automatic) |
| **Anthropic** | `thinking` blocks | `max_tokens`, `refusal` | No (automatic) |
| **Google Gemini** | `thoughtSignature` | `MAX_TOKENS`, `SAFETY` | No (automatic) |
| **DeepSeek/Qwen** | `<think>` tags | OpenAI-compatible | Yes (`EnableReasoning`) |
| **vLLM/GPUStack** | Configurable tags | `length` | Yes (`EnableReasoning`) |

### Enabling Reasoning for DeepSeek/vLLM/Qwen

Some providers require explicit reasoning activation:

```csharp
var client = new ChatClientBuilder(innerClient)
    .UseIndexThinking(o => o.EnableReasoning = true)  // adds the provider's reasoning flag to requests
    .Build(serviceProvider);
```

#### Inline Reasoning Stripping (Automatic)

When reasoning is disabled on continuation requests (to avoid double-billing thinking tokens), open-source models sometimes output inline reasoning text instead of properly tagged content. IndexThinking automatically strips these fragments:

- **`StripLeadingUntaggedReasoning`** — removes reasoning paragraphs that appear at the start of a continuation fragment (≥2 consecutive reasoning paragraphs required before stripping)
- **`StripUntaggedReasoning`** — removes trailing reasoning that appears after the actual answer content (requires ≥200 chars of content before the marker and ≥200 chars of trailing reasoning)

Both methods are called automatically by the continuation pipeline. They are also available as static methods on `OpenSourceReasoningParser` for manual use.

## Documentation

- [Design](docs/DESIGN.md)

## License

MIT License - See [LICENSE](LICENSE) for details.
