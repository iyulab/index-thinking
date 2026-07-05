# IndexThinking

> **Working Memory Manager for Reasoning-capable LLMs**

[![NuGet](https://img.shields.io/nuget/v/IndexThinking.svg)](https://www.nuget.org/packages/IndexThinking)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/)

## What It Does

IndexThinking handles the repetitive-but-hard parts of LLM integration:

- **Truncation Recovery** - Auto-continue when responses hit token limits
- **Reasoning Extraction** - Unified API for provider-specific thinking formats
- **Context Tracking** - Session-aware conversation with sliding window
- **Token Management** - Budget tracking and complexity estimation
- **Content Recovery** - Repair truncated JSON/code blocks

## Scope

IndexThinking manages a **single LLM turn**, not multi-step workflows.

| IndexThinking | Agent Orchestrators |
|---------------|---------------------|
| Single turn optimization | Multi-step coordination |
| Building block | Workflow controller |
| Used BY orchestrators | Uses IndexThinking |

**Token counting role boundary** — IndexThinking owns token *counting* (`ITokenCounter`, framework-neutral; `IChatMessageTokenCounter` for M.E.AI `ChatMessage` counting). Model *metadata* (context window, pricing) belongs to TokenMeter; combining the two into budget enforcement belongs to the consuming pipeline.

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
// Context is automatically tracked and injected
var response = await client.ChatAsync("session-123", "Do that again");
```

### Streaming with Thinking Orchestration

Streaming uses a **Collect-and-Yield** pattern: chunks are yielded to the caller immediately while buffered internally. After the stream completes, the buffered response is processed through the full orchestration pipeline (reasoning parsing, budget tracking, context tracking).

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

```csharp
var options = new ThinkingChatClientOptions
{
    SeparateReasoningInStream = true,        // default false
    // StreamingReasoningStartTag = "<think>",  // defaults shown
    // StreamingReasoningEndTag   = "</think>",
};

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
var options = new ThinkingChatClientOptions
{
    EnableReasoning = true  // Adds include_reasoning: true to requests
};

var client = new ChatClientBuilder(innerClient)
    .UseIndexThinking(options)
    .Build(serviceProvider);
```

#### Inline Reasoning Stripping (Automatic)

When reasoning is disabled on continuation requests (to avoid double-billing thinking tokens), open-source models sometimes output inline reasoning text instead of properly tagged content. IndexThinking automatically strips these fragments:

- **`StripLeadingUntaggedReasoning`** — removes reasoning paragraphs that appear at the start of a continuation fragment (≥2 consecutive reasoning paragraphs required before stripping)
- **`StripUntaggedReasoning`** — removes trailing reasoning that appears after the actual answer content (requires ≥200 chars of content before the marker and ≥200 chars of trailing reasoning)

Both methods are called automatically by the continuation pipeline. They are also available as static methods on `OpenSourceReasoningParser` for manual use.

## Documentation

- [Architecture & Design](docs/ROADMAP.md)
- [Memory Integration](docs/MEMORY_INTEGRATION.md)

## License

MIT License - See [LICENSE](LICENSE) for details.
