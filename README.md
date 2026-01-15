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

## Supported Providers

| Provider | Reasoning Format | Truncation Handling |
|----------|-----------------|---------------------|
| **OpenAI** | `reasoning` field | `length`, `content_filter` |
| **Anthropic** | `thinking` blocks | `max_tokens`, `refusal` |
| **Google Gemini** | `thoughtSignature` | `MAX_TOKENS`, `SAFETY` |
| **DeepSeek/Qwen** | `<think>` tags | OpenAI-compatible |
| **vLLM/GPUStack** | Configurable tags | `length` |

## Documentation

- [Architecture & Design](docs/ROADMAP.md)
- [Memory Integration](docs/MEMORY_INTEGRATION.md)

## License

MIT License - See [LICENSE](LICENSE) for details.
