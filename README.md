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

## Documentation

- [Architecture & Design](docs/ROADMAP.md)
- [Memory Integration](docs/MEMORY_INTEGRATION.md)

## License

MIT License - See [LICENSE](LICENSE) for details.
