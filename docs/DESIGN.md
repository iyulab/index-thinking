# IndexThinking Design

## Core principles

1. **Extend, don't replace** — build on `Microsoft.Extensions.AI` (`IChatClient`), not a parallel abstraction.
2. **Decorator** — `ThinkingChatClient` wraps any `IChatClient` as middleware in a `ChatClientBuilder` pipeline.
3. **Single responsibility** — each component does one thing.
4. **Provider agnostic** — parse the reasoning a response carries; do not couple to one vendor's request API.

## Scope: one LLM turn, not orchestration

IndexThinking is not an agent orchestrator. It optimises a single request/response turn; an orchestrator uses it as a
building block on each call it makes.

| Aspect | IndexThinking | Agent orchestration |
|--------|---------------|---------------------|
| Focus | One LLM turn | Multi-step, multi-agent coordination |
| Responsibility | Reasoning parsing, token budget, truncation detection and recovery | Task routing, tool management, workflow control |
| Input | One request/response pair | A task needing many LLM calls |
| State | Within a turn (thinking state) | Across a conversation or workflow |
| Integration point | Middleware in the `IChatClient` pipeline | Consumes IndexThinking per call |

IndexThinking does: extract thinking/reasoning content from a response · track token budgets and cost · detect a
truncated response and continue it · keep provider reasoning state (signatures, `encrypted_content`) across a turn.

It does not: split a task into several LLM calls · route work between agents · coordinate tool or function calls ·
manage workflow state.

```
Agent orchestrator (any framework)
  ├─ call 1: IChatClient + IndexThinking
  ├─ call 2: IChatClient + IndexThinking
  └─ call 3: IChatClient + IndexThinking
```

## Design decisions

| Decision | Rationale |
|----------|-----------|
| `ThinkingChatClient : DelegatingChatClient` | Matches the .NET AI middleware pattern and composes with other delegating clients |
| `IReasoningParser`, not a request-building adapter | Parsing is the responsibility; building requests is already `IChatClient`'s |
| Turn state lives in `IThinkingStateStore`, apart from the chat client | The chat client stays stateless |
| Budgets are advisory, not enforced | Hard caps on reasoning length degrade answers ("token elasticity", TALE); `IBudgetTracker` reports, it does not cut |
| Standard `ChatMessage` / `ChatResponse` / `ChatOptions` types throughout | No custom request/response types to convert at the boundary |

See [MEMORY_INTEGRATION.md](MEMORY_INTEGRATION.md) for connecting a memory provider.
