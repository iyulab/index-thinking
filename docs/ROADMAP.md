# IndexThinking Roadmap

> v0.1.0 ~ v0.x.x Development Phases (Pre-1.0)

This roadmap outlines the incremental development strategy for IndexThinking, a Working Memory manager for Reasoning-capable LLMs.

## Version Policy (Pre-1.0)

**Major version 1.0은 커뮤니티 검증 완료 후 직접 승격합니다.**

현재 모든 계획은 마이너/패치 버전으로만 진행합니다:
- `0.x.0` - 기능 추가 (breaking changes 가능)
- `0.x.y` - 버그 수정, 개선
- `1.0.0` - 커뮤니티 검증 완료 후 수동 승격 (로드맵에서 계획하지 않음)

---

## Project Philosophy

### Core Principles

1. **Extend, Don't Replace**: Build on top of `Microsoft.Extensions.AI` (`IChatClient`), not parallel abstractions
2. **Decorator Pattern**: `ThinkingChatClient` wraps any `IChatClient` as middleware
3. **Single Responsibility**: Each component does one thing well
4. **Test-First Development**: Every feature has tests before implementation
5. **Provider Agnostic**: Parse reasoning output, don't couple to specific APIs

### Scope Boundaries: IndexThinking vs Agent Orchestration

**IndexThinking는 Agent Orchestration이 아닙니다.** 이 구분은 프로젝트 철학의 핵심입니다.

| Aspect | IndexThinking | Agent Orchestration |
|--------|---------------|---------------------|
| **Focus** | Single LLM turn optimization | Multi-agent/multi-step coordination |
| **Responsibility** | Reasoning parsing, token management, truncation handling | Task routing, tool management, workflow control |
| **Input** | One LLM request/response pair | Complex task requiring multiple LLM calls |
| **Output** | Optimized thinking content + state | Task completion across multiple agents |
| **State Scope** | Within a turn (thinking state) | Across conversation/workflow |
| **Integration Point** | Middleware in IChatClient pipeline | Consumes IndexThinking as a building block |

**IndexThinking이 하는 것:**
- LLM 응답에서 thinking/reasoning 콘텐츠 파싱
- 토큰 예산 관리 및 비용 추적
- 응답 truncation 감지 및 복구
- Provider별 reasoning 상태 보존 (signatures, encrypted_content)

**IndexThinking이 하지 않는 것:**
- 태스크를 여러 LLM 호출로 분해
- 여러 에이전트 간 작업 라우팅
- 도구/함수 호출 조율
- 워크플로우 상태 관리

**관계 예시:**
```
┌─────────────────────────────────────────────────┐
│           Agent Orchestrator                    │
│  (AutoGen, Semantic Kernel, LangGraph, etc.)    │
├─────────────────────────────────────────────────┤
│  Task 1          Task 2          Task 3         │
│    ↓                ↓                ↓          │
│ ┌──────────┐  ┌──────────┐  ┌──────────┐       │
│ │ IChatClient │→│ IChatClient │→│ IChatClient │  │
│ │ + IndexThinking │  │ + IndexThinking │  │ + IndexThinking │  │
│ └──────────┘  └──────────┘  └──────────┘       │
└─────────────────────────────────────────────────┘
```

IndexThinking은 Orchestrator가 각 LLM 호출에서 사용하는 **building block**입니다.

### Architecture Decision Records

| Decision | Rationale |
|----------|-----------|
| Extend `DelegatingChatClient` | Aligns with .NET 10 AI patterns, enables middleware composition |
| `IReasoningParser` not `IReasoningModelAdapter` | Parsing is the core responsibility; request building uses `IChatClient` |
| State stored separately from chat | `IThinkingStateStore` manages turn state; chat client remains stateless |
| Budget is advisory, not enforced | TALE research shows over-constraint causes "Token Elasticity" issues |

### Critical Review Notes

**Original Design Issues Identified:**
- ❌ `IReasoningModelAdapter.BuildContinuationRequest()` duplicates `IChatClient` responsibility
- ❌ `ApiRequest`/`ApiResponse` are custom types when `ChatMessage`/`ChatCompletion` exist
- ❌ No clear integration point with `ChatClientBuilder` middleware pipeline

**Revised Design:**
- ✅ `IReasoningParser` focuses only on extracting thinking content from `ChatCompletion`
- ✅ `ThinkingChatClient : DelegatingChatClient` is the integration point
- ✅ Uses standard `ChatMessage`, `ChatCompletion`, `ChatOptions` types

---

## Phase Overview

| Version | Milestone | Key Deliverables |
|---------|-----------|------------------|
| v0.1.0 | Foundation | Core interfaces, project structure, test infrastructure |
| v0.2.0 | Token Management | Tokenizers, budget calculation, complexity estimation |
| v0.3.0 | Reasoning Parsers I | OpenAI + Anthropic response parsers |
| v0.4.0 | Truncation Handling | Detection, continuation, recovery logic |
| v0.5.0 | State Storage I | InMemory + SQLite state stores |
| v0.6.0 | Reasoning Parsers II | Gemini + DeepSeek/Open-source parsers |
| v0.7.0 | Agent Framework | Internal agents, turn manager, budget tracking |
| v0.7.5 | Client Integration | ThinkingChatClient, UseIndexThinking() |
| v0.8.0 | Memory Integration | IMemoryProvider, FuncMemoryProvider, NullMemoryProvider |
| v0.8.5 | Context-Aware Chat | Conversation tracking, context injection, sliding window |
| v0.9.0 | Context-Integrated Client | ThinkingChatClient context integration |
| v0.10.0 | IDistributedCache | Distributed state storage via .NET abstractions |
| v0.11.0 | Observability | OpenTelemetry integration, custom metrics |
| v0.12.0 | Samples & Demo | Console, Web API sample applications |
| v0.13.0 | Production Hardening | E2E testing, documentation, performance tuning |
| v0.14.0 | Provider Truncation | Provider-specific finish reason handling |
| v0.15.0 | Reasoning Activation | Explicit reasoning activation for DeepSeek/vLLM/Qwen |
| v0.16.0 | Streaming Orchestration | Collect-and-Yield streaming with full thinking pipeline |

---

## v0.1.0 - Foundation (Current Phase)

**Goal**: Establish core interfaces, project structure, and test infrastructure aligned with .NET 10 AI patterns.

### Prerequisites
- .NET 10 SDK
- Understanding of `Microsoft.Extensions.AI` abstractions

### Philosophy Alignment
- Interfaces should be minimal and focused
- All public types should be immutable records where possible
- Async-first design with `CancellationToken` support

### Task Breakdown

#### 1. Project Structure Setup
```
src/
├── IndexThinking/                    # Core library
│   ├── IndexThinking.csproj
│   ├── Abstractions/                 # Interfaces
│   ├── Core/                         # Records, enums
│   └── Extensions/                   # Extension methods
├── IndexThinking.SDK/                # Public API
│   └── IndexThinking.SDK.csproj
tests/
├── IndexThinking.Tests/              # Unit tests
│   └── IndexThinking.Tests.csproj
└── IndexThinking.IntegrationTests/   # Integration tests
    └── IndexThinking.IntegrationTests.csproj
```

- [ ] Create `tests/IndexThinking.Tests` project with xUnit
- [ ] Create `tests/IndexThinking.IntegrationTests` project
- [ ] Add `Directory.Build.props` for shared settings
- [ ] Add `Directory.Packages.props` for Central Package Management
- [ ] Configure `Microsoft.Extensions.AI.Abstractions` dependency

#### 2. Core Interfaces

##### IReasoningParser
**Purpose**: Extract thinking content from `ChatCompletion` responses.

```csharp
/// <summary>
/// Parses reasoning/thinking content from LLM responses.
/// Each provider has different formats (thinking blocks, reasoning tokens, XML tags).
/// </summary>
public interface IReasoningParser
{
    /// <summary>Provider family this parser handles (e.g., "openai", "anthropic").</summary>
    string ProviderFamily { get; }

    /// <summary>Attempts to parse thinking content from a chat completion.</summary>
    bool TryParse(ChatCompletion completion, out ThinkingContent? content);

    /// <summary>Extracts state to preserve for multi-turn conversations.</summary>
    ReasoningState? ExtractState(ChatCompletion completion);
}
```

**Test Cases:**
- [ ] `TryParse_WithValidThinkingBlock_ReturnsTrue`
- [ ] `TryParse_WithNoThinking_ReturnsFalse`
- [ ] `ExtractState_WithEncryptedContent_ReturnsState`
- [ ] `ExtractState_WithSignature_PreservesSignature`

##### IThinkingStateStore
**Purpose**: Persist turn-level thinking state across requests.

```csharp
/// <summary>
/// Stores thinking state for multi-turn conversations and continuation.
/// State is ephemeral (within a turn) but may need persistence for recovery.
/// </summary>
public interface IThinkingStateStore
{
    Task<ThinkingState?> GetAsync(string sessionId, CancellationToken ct = default);
    Task SetAsync(string sessionId, ThinkingState state, CancellationToken ct = default);
    Task RemoveAsync(string sessionId, CancellationToken ct = default);
    Task<bool> ExistsAsync(string sessionId, CancellationToken ct = default);
}
```

**Test Cases:**
- [ ] `GetAsync_NonExistent_ReturnsNull`
- [ ] `SetAsync_ThenGet_ReturnsSameState`
- [ ] `RemoveAsync_ThenGet_ReturnsNull`
- [ ] `SetAsync_Overwrites_ExistingState`
- [ ] `Operations_WithCancellation_ThrowsOperationCanceledException`

##### ITruncationDetector
**Purpose**: Detect if a response was truncated.

```csharp
/// <summary>
/// Detects if a chat completion was truncated due to token limits.
/// </summary>
public interface ITruncationDetector
{
    /// <summary>Determines if the completion was truncated.</summary>
    TruncationInfo Detect(ChatCompletion completion);
}
```

**Test Cases:**
- [ ] `Detect_FinishReasonLength_ReturnsTruncated`
- [ ] `Detect_FinishReasonStop_ReturnsNotTruncated`
- [ ] `Detect_UnbalancedBraces_ReturnsTruncated`
- [ ] `Detect_IncompleteCodeBlock_ReturnsTruncated`
- [ ] `Detect_MidSentence_ReturnsTruncated`

##### ITokenCounter
**Purpose**: Count tokens for budget management.

```csharp
/// <summary>
/// Counts tokens in text for budget management.
/// Supports model-specific tokenizers with fallback chain.
/// </summary>
public interface ITokenCounter
{
    /// <summary>Counts tokens in the given text.</summary>
    int Count(string text);

    /// <summary>Counts tokens for a chat message (includes role overhead).</summary>
    int Count(ChatMessage message);

    /// <summary>Indicates if this counter supports the specified model.</summary>
    bool SupportsModel(string modelId);
}
```

**Test Cases:**
- [ ] `Count_EmptyString_ReturnsZero`
- [ ] `Count_EnglishText_ReturnsApproximateTokens`
- [ ] `Count_KoreanText_ReturnsHigherTokenRatio`
- [ ] `Count_ChatMessage_IncludesRoleOverhead`
- [ ] `SupportsModel_UnknownModel_ReturnsFalse`

#### 3. Core Record Types

```csharp
/// <summary>Parsed thinking/reasoning content from an LLM response.</summary>
public sealed record ThinkingContent
{
    /// <summary>The thinking/reasoning text (may be summarized).</summary>
    public required string Text { get; init; }

    /// <summary>Number of tokens used for thinking.</summary>
    public int TokenCount { get; init; }

    /// <summary>Whether this is a summary or full thinking trace.</summary>
    public bool IsSummarized { get; init; }
}

/// <summary>Provider-specific state to preserve across turns.</summary>
public sealed record ReasoningState
{
    /// <summary>Provider family (openai, anthropic, gemini, etc.).</summary>
    public required string Provider { get; init; }

    /// <summary>Opaque state data (encrypted_content, signatures, etc.).</summary>
    public required byte[] Data { get; init; }

    /// <summary>When this state was captured.</summary>
    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Complete thinking state for a session.</summary>
public sealed record ThinkingState
{
    public required string SessionId { get; init; }
    public string? ModelId { get; init; }
    public ReasoningState? ReasoningState { get; init; }
    public int TotalThinkingTokens { get; init; }
    public int TotalOutputTokens { get; init; }
    public int ContinuationCount { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Result of truncation detection.</summary>
public sealed record TruncationInfo
{
    public required bool IsTruncated { get; init; }
    public TruncationReason Reason { get; init; }
    public string? Details { get; init; }
}

public enum TruncationReason
{
    None,
    TokenLimit,
    UnbalancedStructure,
    IncompleteCodeBlock,
    MidSentence
}

/// <summary>Token budget configuration.</summary>
public sealed record BudgetConfig
{
    public int ThinkingBudget { get; init; } = 4096;
    public int AnswerBudget { get; init; } = 4096;
    public int MaxContinuations { get; init; } = 5;
    public TimeSpan MaxDuration { get; init; } = TimeSpan.FromMinutes(10);
}

/// <summary>Task complexity for budget estimation.</summary>
public enum TaskComplexity
{
    Simple,     // Quick factual answers
    Moderate,   // Code explanation, summaries
    Complex,    // Bug fixing, analysis
    Research    // Multi-step deep research
}
```

**Test Cases for Records:**
- [ ] `ThinkingContent_Equality_WorksCorrectly`
- [ ] `ThinkingState_WithExpression_CreatesNewInstance`
- [ ] `BudgetConfig_DefaultValues_AreReasonable`
- [ ] `Records_Serialization_RoundTrips` (System.Text.Json)

#### 4. Configuration Types

```csharp
/// <summary>Configuration options for IndexThinking.</summary>
public sealed class IndexThinkingOptions
{
    /// <summary>Default model to use if not specified.</summary>
    public string? DefaultModel { get; set; }

    /// <summary>Default budget configuration.</summary>
    public BudgetConfig DefaultBudget { get; set; } = new();

    /// <summary>Whether to enable automatic continuation.</summary>
    public bool EnableAutoContinuation { get; set; } = true;

    /// <summary>Whether to store thinking state.</summary>
    public bool EnableStateStorage { get; set; } = true;
}
```

#### 5. Extension Methods

```csharp
public static class ChatClientExtensions
{
    /// <summary>
    /// Wraps the chat client with IndexThinking capabilities.
    /// </summary>
    public static IChatClient UseIndexThinking(
        this IChatClient client,
        Action<IndexThinkingOptions>? configure = null);
}
```

**Test Cases:**
- [ ] `UseIndexThinking_WithDefaults_ReturnsWrappedClient`
- [ ] `UseIndexThinking_WithOptions_AppliesConfiguration`
- [ ] `UseIndexThinking_ChainedWithOtherMiddleware_Works`

### Verification Criteria

| Criteria | Verification Method |
|----------|---------------------|
| All interfaces compile | `dotnet build` succeeds |
| Records are immutable | Compiler enforces `init` |
| Tests are discoverable | `dotnet test --list-tests` shows all tests |
| Code coverage baseline | `>80%` coverage on new code |
| XML docs complete | No CS1591 warnings |

### Test Infrastructure

```csharp
// Example test structure
public class ThinkingStateStoreTests
{
    [Fact]
    public async Task GetAsync_NonExistent_ReturnsNull()
    {
        // Arrange
        var store = CreateStore();

        // Act
        var result = await store.GetAsync("non-existent");

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("session-1")]
    [InlineData("session-with-special-chars-!@#")]
    public async Task SetAsync_ThenGet_ReturnsSameState(string sessionId)
    {
        // Arrange
        var store = CreateStore();
        var state = CreateTestState(sessionId);

        // Act
        await store.SetAsync(sessionId, state);
        var result = await store.GetAsync(sessionId);

        // Assert
        Assert.Equal(state, result);
    }

    protected virtual IThinkingStateStore CreateStore()
        => new InMemoryThinkingStateStore();

    private static ThinkingState CreateTestState(string sessionId) => new()
    {
        SessionId = sessionId,
        ModelId = "test-model",
        TotalThinkingTokens = 100
    };
}
```

### Deliverables

| Deliverable | Location | Verification |
|-------------|----------|--------------|
| Core interfaces | `src/IndexThinking/Abstractions/` | Compiles |
| Record types | `src/IndexThinking/Core/` | Unit tests pass |
| Test project | `tests/IndexThinking.Tests/` | All tests green |
| Project structure | Solution root | `dotnet build` succeeds |

### Acceptance Criteria

- [ ] `dotnet build IndexThinking.slnx` succeeds with no warnings
- [ ] `dotnet test` runs all tests successfully
- [ ] Code coverage report shows >80% on new code
- [ ] No TODO comments in committed code
- [ ] All public types have XML documentation

---

## v0.2.0 - Token Management (Current Phase)

**Goal**: Implement accurate token counting with fallback chain strategy.

### Prerequisites
- v0.1.0 Foundation complete
- Microsoft.ML.Tokenizers package (v1.0.2)

### Philosophy Alignment
- **Single Responsibility**: Counters only count tokens; budget decisions elsewhere
- **Fallback Chain**: Exact → Encoding-based → Approximate
- **Provider Agnostic**: Support any model through approximation

### Critical Review Notes

**Scope Refinement (from original plan):**
- ✅ TokenCounterChain with fallback strategy
- ✅ Microsoft.ML.Tokenizers integration (o200k_base, cl100k_base)
- ✅ Language-aware approximate counting
- ❌ ComplexityEstimator → Moved to v0.4.0 (uses counters, not part of counting)
- ❌ BudgetAllocator → Moved to v0.4.0 (budget decisions, not counting)
- ❌ Model token limit registry → Lives in BudgetConfig (v0.4.0)

**Rationale**: Token counting is infrastructure; budget allocation is a consumer.

### Task Breakdown

#### 1. TiktokenTokenCounter
Exact token counting for OpenAI models.

```csharp
public sealed class TiktokenTokenCounter : ITokenCounter
{
    private readonly TiktokenTokenizer _tokenizer;
    private const int MessageOverhead = 4; // role + formatting

    public int Count(string text) => _tokenizer.CountTokens(text);
    public int Count(ChatMessage message) => Count(message.Text ?? "") + MessageOverhead;
    public bool SupportsModel(string modelId) => ModelEncodingRegistry.IsOpenAIModel(modelId);
}
```

**Test Cases:**
- `Count_EmptyString_ReturnsZero`
- `Count_HelloWorld_Returns2Tokens` (verified with official tiktoken)
- `Count_KoreanText_ReturnsExpectedTokens`
- `Count_ChatMessage_IncludesRoleOverhead`
- `SupportsModel_Gpt4o_ReturnsTrue`
- `SupportsModel_Claude_ReturnsFalse`

#### 2. ApproximateTokenCounter
Fallback counter using character/token ratios.

```csharp
public sealed class ApproximateTokenCounter : ITokenCounter
{
    public record LanguageRatios(
        double English = 4.0,    // ~4 chars/token
        double Korean = 1.5,     // More tokens per char
        double Chinese = 1.2,
        double Default = 3.5);   // Conservative

    public bool SupportsModel(string modelId) => true; // Always fallback
}
```

**Test Cases:**
- `Count_EnglishText_ReturnsApproximateTokens`
- `Count_KoreanText_ReturnsHigherTokenCount`
- `Count_MixedLanguage_UsesBlendedRatio`
- `SupportsModel_AnyModel_ReturnsTrue`

#### 3. TokenCounterChain
Try counters in order until one supports the model.

```csharp
public sealed class TokenCounterChain : ITokenCounter
{
    private readonly IReadOnlyList<ITokenCounter> _counters;
    private string? _currentModelId;

    public TokenCounterChain WithModel(string modelId);
}
```

**Test Cases:**
- `Count_WithSupportedModel_UsesFirstMatch`
- `Count_WithUnsupportedModel_UsesFallback`
- `Chain_EmptyCounters_Throws`

#### 4. ModelEncodingRegistry
Map model IDs to tiktoken encodings.

```csharp
public static class ModelEncodingRegistry
{
    // o200k_base: gpt-4o, o1, o3, o4-mini
    // cl100k_base: gpt-4, gpt-3.5-turbo
    public static string? GetEncoding(string modelId);
    public static bool IsOpenAIModel(string modelId);
}
```

**Test Cases:**
- `GetEncoding_Gpt4o_ReturnsO200kBase`
- `GetEncoding_Gpt4_ReturnsCl100kBase`
- `GetEncoding_Claude_ReturnsNull`
- `IsOpenAIModel_Gpt4o_ReturnsTrue`

#### 5. TokenCounterFactory
Create appropriate counter for a model.

**Test Cases:**
- `Create_WithGpt4o_ReturnsTiktokenFirst`
- `Create_WithClaude_ReturnsApproximateOnly`
- `Create_WithNull_ReturnsDefaultChain`

### Verification Criteria

| Criteria | Method |
|----------|--------|
| Tiktoken accuracy | 0% difference from Python tiktoken |
| Approximate accuracy | Within ±20% of exact |
| All tests pass | `dotnet test` succeeds |
| Code coverage | >90% on new code |

### Project Structure

```
src/IndexThinking/Tokenization/
├── TiktokenTokenCounter.cs
├── ApproximateTokenCounter.cs
├── TokenCounterChain.cs
├── TokenCounterFactory.cs
├── ModelEncodingRegistry.cs
└── LanguageRatios.cs
```

### Deliverables
- `IndexThinking.Tokenization` namespace with full implementation
- Unit tests with >90% coverage
- Verified against official tiktoken

---

## v0.3.0 - Reasoning Parsers I (OpenAI + Anthropic)

**Goal**: Implement parsers for the two primary commercial reasoning models.

### Tasks

#### OpenAI Parser
- [ ] Parse Responses API reasoning output
- [ ] Extract `encrypted_content` for state preservation
- [ ] Handle `reasoning.effort` and `reasoning.summary`

#### Anthropic Parser
- [ ] Parse Extended Thinking `thinking` blocks
- [ ] Preserve thinking blocks with signatures
- [ ] Handle `redacted_thinking` blocks

### Test Requirements
- [ ] Parser tests with real response samples (anonymized)
- [ ] State extraction tests
- [ ] Round-trip serialization tests

### Deliverables
- `IndexThinking.Parsers.OpenAI`
- `IndexThinking.Parsers.Anthropic`

---

## v0.4.0 - Truncation Handling

**Goal**: Detect response truncation and implement seamless continuation.

### Tasks

- [ ] Implement `TruncationDetector`
- [ ] Create `ContinuationConfig`
- [ ] Implement continuation loop with guards
- [ ] Create continuation prompt templates
- [ ] Implement JSON/code block recovery

### Test Requirements
- [ ] Truncation detection tests for all finish reasons
- [ ] Continuation loop tests with mock clients
- [ ] JSON recovery tests with various malformed inputs
- [ ] Infinite loop prevention tests

### Deliverables
- `IndexThinking.Continuation` namespace

---

## v0.5.0 - State Storage I (InMemory + SQLite)

**Goal**: Implement basic state persistence.

### Tasks
- [ ] Implement `InMemoryThinkingStateStore`
- [ ] Implement `SqliteThinkingStateStore`
- [ ] Create state serialization logic
- [ ] Add DI registration extensions

### Test Requirements
- [ ] Abstract base test class for all stores
- [ ] Concurrency tests
- [ ] TTL/expiration tests

### Deliverables
- `IndexThinking.Storage.Memory`
- `IndexThinking.Storage.Sqlite`

---

## v0.6.0 - Reasoning Parsers II (Gemini + DeepSeek)

**Goal**: Extend parser support to more providers.

### Tasks
- [ ] Gemini parser with `thought_signature` handling
- [ ] DeepSeek/Qwen parser for `<think>` tags
- [ ] vLLM `reasoning_format: parsed` support

### Test Requirements
- [ ] Parser tests with real response samples

### Deliverables
- `IndexThinking.Parsers.Gemini`
- `IndexThinking.Parsers.OpenSource`

---

## v0.7.0 - Internal Thinking Agents

**Goal**: Implement internal components for single-turn thinking optimization.

> **IMPORTANT**: These are NOT external agent orchestrators (like AutoGen, LangGraph).
> These are internal components that optimize a SINGLE LLM turn's thinking process.
> See "Scope Boundaries" in Philosophy section.

### Prerequisites
- v0.6.0 Reasoning Parsers II complete
- Understanding of existing components:
  - `ITokenCounter` / `TokenCounterChain` (v0.2.0)
  - `ITruncationDetector` / `TruncationDetector` (v0.4.0)
  - `ContentRecoveryUtils` (v0.4.0)
  - `ContinuationConfig` (v0.4.0)
  - `BudgetConfig`, `TaskComplexity` (v0.1.0)

### Philosophy Alignment

**Research Foundation:**
- [TALE (Token-Budget-Aware LLM Reasoning)](https://arxiv.org/abs/2412.18547) - ACL 2025
- [SelfBudgeter](https://arxiv.org/abs/2505.11274) - Adaptive token allocation
- [Reasoning on a Budget Survey](https://arxiv.org/abs/2507.02076) - Adaptive test-time compute

**Key Principles from Research:**
1. **Budget is Advisory, Not Enforced** - TALE shows over-constraint causes "Token Elasticity" issues
2. **Complexity-Based Allocation** - Different tasks need different budgets (TALE-EP approach)
3. **State Machine Continuation** - Iterate until `finish_reason != "length"` (best practice)
4. **Progress Tracking** - Prevent infinite loops with minimum progress thresholds

**What v0.7.0 Does:**
- Compose existing components into cohesive turn management
- Estimate task complexity for budget recommendations
- Manage continuation loop with guards
- Track turn-level metrics (tokens, continuations, duration)

**What v0.7.0 Does NOT Do:**
- Train or fine-tune models (that's TALE-PT/SelfBudgeter territory)
- Route between multiple LLM calls (that's Agent Orchestration)
- Inject budget into model prompts (consumer's responsibility)

### Task Breakdown

#### 1. Core Types

##### ThinkingContext
```csharp
/// <summary>
/// Contextual information for a single thinking turn.
/// Passed between internal components during turn processing.
/// </summary>
public sealed record ThinkingContext
{
    /// <summary>Unique identifier for this turn.</summary>
    public required string TurnId { get; init; }

    /// <summary>Session identifier for state correlation.</summary>
    public required string SessionId { get; init; }

    /// <summary>The original user request.</summary>
    public required IList<ChatMessage> Messages { get; init; }

    /// <summary>Budget configuration for this turn.</summary>
    public BudgetConfig Budget { get; init; } = new();

    /// <summary>Continuation configuration.</summary>
    public ContinuationConfig Continuation { get; init; } = ContinuationConfig.Default;

    /// <summary>Estimated task complexity.</summary>
    public TaskComplexity? EstimatedComplexity { get; init; }

    /// <summary>Model identifier (if known).</summary>
    public string? ModelId { get; init; }

    /// <summary>When this turn started.</summary>
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Cancellation token for this turn.</summary>
    public CancellationToken CancellationToken { get; init; }
}
```

##### TurnResult
```csharp
/// <summary>
/// Result of a completed thinking turn.
/// </summary>
public sealed record TurnResult
{
    /// <summary>The final (possibly combined) response.</summary>
    public required ChatResponse Response { get; init; }

    /// <summary>Parsed thinking content, if any.</summary>
    public ThinkingContent? ThinkingContent { get; init; }

    /// <summary>Provider-specific reasoning state.</summary>
    public ReasoningState? ReasoningState { get; init; }

    /// <summary>Metrics for this turn.</summary>
    public required TurnMetrics Metrics { get; init; }

    /// <summary>Whether continuation was needed.</summary>
    public bool WasContinued => Metrics.ContinuationCount > 0;

    /// <summary>Whether the response was truncated (max continuations reached).</summary>
    public bool WasTruncated { get; init; }
}

/// <summary>
/// Metrics collected during a thinking turn.
/// </summary>
public sealed record TurnMetrics
{
    public int InputTokens { get; init; }
    public int ThinkingTokens { get; init; }
    public int OutputTokens { get; init; }
    public int ContinuationCount { get; init; }
    public TimeSpan Duration { get; init; }
    public TaskComplexity? DetectedComplexity { get; init; }
}
```

**Test Cases:**
- [ ] `ThinkingContext_Defaults_AreReasonable`
- [ ] `TurnResult_WasContinued_ReflectsContinuationCount`
- [ ] `TurnMetrics_Serialization_RoundTrips`

#### 2. Complexity Estimator

##### IComplexityEstimator
```csharp
/// <summary>
/// Estimates task complexity from input messages.
/// Used to recommend appropriate token budgets.
/// </summary>
public interface IComplexityEstimator
{
    /// <summary>
    /// Estimates the complexity of a task based on input messages.
    /// </summary>
    TaskComplexity Estimate(IReadOnlyList<ChatMessage> messages);

    /// <summary>
    /// Gets recommended budget for a given complexity level.
    /// </summary>
    BudgetConfig GetRecommendedBudget(TaskComplexity complexity);
}
```

##### HeuristicComplexityEstimator
Heuristic-based estimator using:
- Message length and count
- Keyword detection (debug, analyze, research, explain, etc.)
- Code block presence
- Question complexity markers

```csharp
public sealed class HeuristicComplexityEstimator : IComplexityEstimator
{
    private readonly ITokenCounter _tokenCounter;

    // Complexity signals
    private static readonly string[] ResearchKeywords =
        ["research", "investigate", "comprehensive", "in-depth", "analyze thoroughly"];
    private static readonly string[] ComplexKeywords =
        ["debug", "fix", "refactor", "optimize", "implement", "design"];
    private static readonly string[] ModerateKeywords =
        ["explain", "summarize", "describe", "compare", "list"];
}
```

**Test Cases:**
- [ ] `Estimate_ShortFactualQuestion_ReturnsSimple`
- [ ] `Estimate_CodeWithDebugKeyword_ReturnsComplex`
- [ ] `Estimate_ResearchQuestion_ReturnsResearch`
- [ ] `Estimate_ModerateLengthExplanation_ReturnsModerate`
- [ ] `GetRecommendedBudget_Simple_ReturnsSmallBudget`
- [ ] `GetRecommendedBudget_Research_ReturnsLargeBudget`

#### 3. Continuation Handler

##### IContinuationHandler
```csharp
/// <summary>
/// Handles response continuation when truncation is detected.
/// </summary>
public interface IContinuationHandler
{
    /// <summary>
    /// Processes a response and continues if truncated.
    /// </summary>
    /// <param name="context">The thinking context.</param>
    /// <param name="initialResponse">The initial (possibly truncated) response.</param>
    /// <param name="sendRequest">Function to send continuation requests.</param>
    /// <returns>The final combined response with metrics.</returns>
    Task<ContinuationResult> HandleAsync(
        ThinkingContext context,
        ChatResponse initialResponse,
        Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>> sendRequest);
}

/// <summary>
/// Result of continuation handling.
/// </summary>
public sealed record ContinuationResult
{
    public required ChatResponse FinalResponse { get; init; }
    public required int ContinuationCount { get; init; }
    public required bool ReachedMaxContinuations { get; init; }
    public IReadOnlyList<ChatResponse> IntermediateResponses { get; init; } = [];
}
```

##### DefaultContinuationHandler
Implements state-machine pattern:
1. Check if response is truncated (via ITruncationDetector)
2. If not truncated, return immediately
3. If truncated, apply recovery (via ContentRecoveryUtils)
4. Build continuation request with previous response
5. Loop until complete or max continuations reached
6. Combine fragments into final response

```csharp
public sealed class DefaultContinuationHandler : IContinuationHandler
{
    private readonly ITruncationDetector _truncationDetector;
    private readonly ILogger<DefaultContinuationHandler>? _logger;

    // Uses ContinuationConfig from context
    // Tracks progress to prevent infinite loops
    // Combines fragments using ContentRecoveryUtils
}
```

**Test Cases:**
- [ ] `HandleAsync_NotTruncated_ReturnsImmediately`
- [ ] `HandleAsync_TruncatedOnce_ContinuesAndCombines`
- [ ] `HandleAsync_MultipleTruncations_ContinuesUntilComplete`
- [ ] `HandleAsync_MaxContinuationsReached_StopsAndReturnsPartial`
- [ ] `HandleAsync_NoProgress_StopsEarly`
- [ ] `HandleAsync_Cancelled_ThrowsOperationCancelled`
- [ ] `HandleAsync_JsonTruncated_RecoveryApplied`

#### 4. Budget Tracker

##### IBudgetTracker
```csharp
/// <summary>
/// Tracks token usage within a thinking turn.
/// Advisory only - does not enforce limits.
/// </summary>
public interface IBudgetTracker
{
    /// <summary>Records tokens from a response.</summary>
    void RecordResponse(ChatResponse response, ThinkingContent? thinking);

    /// <summary>Gets current usage metrics.</summary>
    BudgetUsage GetUsage();

    /// <summary>Checks if thinking budget is exceeded (advisory).</summary>
    bool IsThinkingBudgetExceeded(BudgetConfig config);

    /// <summary>Checks if answer budget is exceeded (advisory).</summary>
    bool IsAnswerBudgetExceeded(BudgetConfig config);
}

/// <summary>
/// Current budget usage within a turn.
/// </summary>
public sealed record BudgetUsage
{
    public int InputTokens { get; init; }
    public int ThinkingTokens { get; init; }
    public int OutputTokens { get; init; }
    public int TotalTokens => InputTokens + ThinkingTokens + OutputTokens;
}
```

##### DefaultBudgetTracker
```csharp
public sealed class DefaultBudgetTracker : IBudgetTracker
{
    private readonly ITokenCounter _tokenCounter;
    private int _inputTokens;
    private int _thinkingTokens;
    private int _outputTokens;

    // Thread-safe accumulation
    // Extracts token counts from response.Usage if available
    // Falls back to ITokenCounter for estimation
}
```

**Test Cases:**
- [ ] `RecordResponse_WithUsageMetadata_UsesActualCounts`
- [ ] `RecordResponse_WithoutUsage_EstimatesTokens`
- [ ] `GetUsage_AfterMultipleResponses_AccumulatesCorrectly`
- [ ] `IsThinkingBudgetExceeded_UnderBudget_ReturnsFalse`
- [ ] `IsThinkingBudgetExceeded_OverBudget_ReturnsTrue`

#### 5. Turn Manager

##### IThinkingTurnManager
```csharp
/// <summary>
/// Coordinates a single thinking turn from request to result.
/// This is the main entry point for turn-level processing.
/// </summary>
public interface IThinkingTurnManager
{
    /// <summary>
    /// Processes a complete thinking turn.
    /// </summary>
    /// <param name="context">The thinking context.</param>
    /// <param name="sendRequest">Function to send requests to the LLM.</param>
    /// <returns>The turn result with response and metrics.</returns>
    Task<TurnResult> ProcessTurnAsync(
        ThinkingContext context,
        Func<IList<ChatMessage>, CancellationToken, Task<ChatResponse>> sendRequest);
}
```

##### DefaultThinkingTurnManager
Orchestrates all components:
1. Estimate complexity (if not provided)
2. Send initial request
3. Parse reasoning content
4. Handle continuation if needed
5. Track budget usage
6. Store state (via IThinkingStateStore)
7. Return combined result

```csharp
public sealed class DefaultThinkingTurnManager : IThinkingTurnManager
{
    private readonly IComplexityEstimator _complexityEstimator;
    private readonly IContinuationHandler _continuationHandler;
    private readonly IReasoningParser[] _parsers;
    private readonly IThinkingStateStore _stateStore;
    private readonly ILogger<DefaultThinkingTurnManager>? _logger;

    public async Task<TurnResult> ProcessTurnAsync(...)
    {
        // 1. Estimate complexity
        var complexity = context.EstimatedComplexity
            ?? _complexityEstimator.Estimate(context.Messages);

        // 2. Create budget tracker
        var budgetTracker = new DefaultBudgetTracker(_tokenCounter);

        // 3. Send initial request
        var response = await sendRequest(context.Messages, context.CancellationToken);

        // 4. Handle continuation if needed
        var continuationResult = await _continuationHandler.HandleAsync(
            context, response, sendRequest);

        // 5. Parse reasoning from final response
        var (thinkingContent, reasoningState) = ParseReasoning(continuationResult.FinalResponse);

        // 6. Record final metrics
        budgetTracker.RecordResponse(continuationResult.FinalResponse, thinkingContent);

        // 7. Store state
        await StoreStateAsync(context, budgetTracker, continuationResult);

        // 8. Return result
        return new TurnResult { ... };
    }
}
```

**Test Cases:**
- [ ] `ProcessTurnAsync_SimpleRequest_CompletesWithoutContinuation`
- [ ] `ProcessTurnAsync_TruncatedResponse_ContinuesSuccessfully`
- [ ] `ProcessTurnAsync_WithThinkingContent_ParsesCorrectly`
- [ ] `ProcessTurnAsync_StoresState_InStateStore`
- [ ] `ProcessTurnAsync_TracksMetrics_Accurately`
- [ ] `ProcessTurnAsync_Cancelled_ThrowsAndCleansUp`

#### 6. DI Extensions

```csharp
public static class AgentServiceCollectionExtensions
{
    /// <summary>
    /// Adds IndexThinking turn management services.
    /// </summary>
    public static IServiceCollection AddIndexThinkingAgents(
        this IServiceCollection services,
        Action<AgentOptions>? configure = null)
    {
        services.TryAddSingleton<IComplexityEstimator, HeuristicComplexityEstimator>();
        services.TryAddSingleton<IContinuationHandler, DefaultContinuationHandler>();
        services.TryAddSingleton<IThinkingTurnManager, DefaultThinkingTurnManager>();

        // Configure options
        if (configure is not null)
        {
            services.Configure(configure);
        }

        return services;
    }
}

public sealed class AgentOptions
{
    /// <summary>Default budget configuration.</summary>
    public BudgetConfig DefaultBudget { get; set; } = new();

    /// <summary>Default continuation configuration.</summary>
    public ContinuationConfig DefaultContinuation { get; set; } = ContinuationConfig.Default;

    /// <summary>Whether to auto-estimate complexity.</summary>
    public bool AutoEstimateComplexity { get; set; } = true;
}
```

**Test Cases:**
- [ ] `AddIndexThinkingAgents_RegistersAllServices`
- [ ] `AddIndexThinkingAgents_WithOptions_AppliesConfiguration`
- [ ] `Services_ResolveCorrectly_FromDI`

### Project Structure

```
src/IndexThinking/
├── Agents/
│   ├── IComplexityEstimator.cs
│   ├── HeuristicComplexityEstimator.cs
│   ├── IContinuationHandler.cs
│   ├── DefaultContinuationHandler.cs
│   ├── IBudgetTracker.cs
│   ├── DefaultBudgetTracker.cs
│   ├── IThinkingTurnManager.cs
│   ├── DefaultThinkingTurnManager.cs
│   ├── ThinkingContext.cs
│   ├── TurnResult.cs
│   └── AgentOptions.cs
├── Extensions/
│   └── AgentServiceCollectionExtensions.cs (new)
```

### Verification Criteria

| Criteria | Method |
|----------|--------|
| All interfaces compile | `dotnet build` succeeds |
| Unit tests pass | `dotnet test` succeeds |
| Code coverage | >85% on new code |
| Integration with existing components | Uses ITruncationDetector, ITokenCounter, etc. |
| No breaking changes | Existing APIs unchanged |

### Test Summary

| Component | Test Count | Coverage Target |
|-----------|------------|-----------------|
| ThinkingContext/TurnResult | 5 | Records, serialization |
| HeuristicComplexityEstimator | 6 | All complexity levels |
| DefaultContinuationHandler | 7 | All continuation scenarios |
| DefaultBudgetTracker | 5 | Token tracking |
| DefaultThinkingTurnManager | 6 | Full turn processing |
| DI Extensions | 3 | Service registration |
| **Total** | **32+** | **>85%** |

### Deliverables

| Deliverable | Location | Verification |
|-------------|----------|--------------|
| Core types | `src/IndexThinking/Agents/` | Compiles |
| Complexity estimator | `src/IndexThinking/Agents/` | Unit tests |
| Continuation handler | `src/IndexThinking/Agents/` | Unit tests |
| Budget tracker | `src/IndexThinking/Agents/` | Unit tests |
| Turn manager | `src/IndexThinking/Agents/` | Integration tests |
| DI extensions | `src/IndexThinking/Extensions/` | Unit tests |
| Test suite | `tests/IndexThinking.Tests/Agents/` | All green |

### Acceptance Criteria

- [ ] `dotnet build IndexThinking.slnx` succeeds with no warnings
- [ ] `dotnet test` runs all tests successfully (450+ tests expected)
- [ ] Code coverage >85% on new code
- [ ] No TODO comments in committed code
- [ ] All public types have XML documentation
- [ ] Existing v0.1-v0.6 tests still pass

---

## v0.8.0 - Memory Integration

**Goal**: Integrate with Memory-Indexer for long-term memory recall.

### Tasks
- [ ] Define `IMemoryProvider` interface
- [ ] Implement `MemoryAgent`
- [ ] Create memory-aware context builder
- [ ] Add fact extraction pipeline

### Test Requirements
- [ ] Memory recall integration tests
- [ ] Fact extraction accuracy tests

### Deliverables
- `IndexThinking.Memory` namespace

---

## v0.8.5 - Context-Aware Chat Infrastructure

**Goal**: Provide conversation context to LLM for natural reference resolution.

### Philosophy (Revised from Original Plan)

**Original approach** (IQueryEnricher, pronoun resolution):
- Programmatically resolve "this", "that thing" → explicit entities
- Extra LLM calls or rule-based interpretation

**Critical insight** from LangChain/LangChain4j research:
- LLMs are excellent at understanding conversational context
- Programmatic pronoun resolution is limited and error-prone
- Extra LLM calls violate zero-config philosophy

**New approach** (Context Injection):
- Track conversation history with sliding window
- Inject previous turns as context to LLM
- Let LLM naturally understand "Do that thing again"
- Zero additional LLM calls

### Core Interfaces

```csharp
/// <summary>
/// Tracks conversation context within a session.
/// </summary>
public interface IContextTracker
{
    void Track(string sessionId, ChatMessage userMessage, ChatResponse? response);
    ConversationContext GetContext(string sessionId);
    void Clear(string sessionId);
    bool HasContext(string sessionId);
}

/// <summary>
/// Injects conversation context into messages for context-aware LLM processing.
/// </summary>
public interface IContextInjector
{
    IList<ChatMessage> InjectContext(IList<ChatMessage> messages, ConversationContext context);
}

public sealed record ConversationTurn
{
    public required ChatMessage UserMessage { get; init; }
    public ChatResponse? AssistantResponse { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string TurnId { get; init; }
}

public sealed record ConversationContext
{
    public required string SessionId { get; init; }
    public IReadOnlyList<ConversationTurn> RecentTurns { get; init; }
    public int TotalTurnCount { get; init; }
    public bool HasHistory => RecentTurns.Count > 0;
}
```

### Tasks
- [x] Define `IContextTracker` interface
- [x] Implement `InMemoryContextTracker` with sliding window
- [x] Define `IContextInjector` interface
- [x] Implement `DefaultContextInjector` (prepends history as messages)
- [x] Create `ConversationTurn` and `ConversationContext` types
- [x] Add DI extensions: `AddIndexThinkingContext()`
- [x] Unit tests for all components

### Configuration

```csharp
services.AddIndexThinkingContext(
    trackerOptions =>
    {
        trackerOptions.MaxTurns = 10;                    // Sliding window size
        trackerOptions.SessionTtl = TimeSpan.FromHours(1);
        trackerOptions.EnableCleanupTimer = true;
    },
    injectorOptions =>
    {
        injectorOptions.EnableInjection = true;
        injectorOptions.MaxTurnsToInject = 5;            // Limit context size
    });
```

### Deliverables
- `IndexThinking.Context` namespace with full implementation
- `IndexThinking.Abstractions.IContextTracker` interface
- `IndexThinking.Abstractions.IContextInjector` interface
- DI extensions in `ServiceCollectionExtensions`

### Deferred to Later Versions
- `IFollowUpGenerator` → v0.9.x (separate concern)
- Memory-enhanced context → Uses existing IMemoryProvider integration

---

## v0.9.0 - Context-Integrated Client

**Goal**: Integrate conversation context into ThinkingChatClient for seamless session-aware chat.

### Philosophy (Revised from Original Plan)

**Original approach** (IThinkingService facade):
- Create new `IThinkingService` interface
- Separate abstraction from IChatClient

**Critical insight** from M.E.AI patterns:
- Microsoft.Extensions.AI already defines IChatClient as the standard
- Adding another facade violates "Extend, Don't Replace" principle
- Semantic Kernel builds ON IChatClient, not beside it

**New approach** (Context-Integrated Client):
- Enhance existing `ThinkingChatClient` with context support
- Add convenience extension methods for common patterns
- Use DI to automatically inject context services

### API Design

```csharp
// Simple session-aware chat (convenience method)
var response = await client.ChatAsync("session-123", "Do that again");

// Or with explicit options
var options = ThinkingChatClientExtensions.WithSession("session-123");
var response = await client.GetResponseAsync(messages, options);

// Full control with DI
services.AddIndexThinkingAgents();
services.AddIndexThinkingContext();

var client = new ChatClientBuilder(innerClient)
    .UseIndexThinking()  // Auto-resolves IContextTracker, IContextInjector
    .Build();
```

### Tasks
- [x] Integrate IContextTracker/IContextInjector into ThinkingChatClient
- [x] Add context options to ThinkingChatClientOptions
- [x] Update UseIndexThinking() to resolve context services from DI
- [x] Add convenience methods: ChatAsync(), SendAsync(), WithSession()
- [x] Add comprehensive unit tests

### Configuration

```csharp
.UseIndexThinking(options =>
{
    options.EnableContextTracking = true;
    options.EnableContextInjection = true;
    options.MaxContextTurns = 5;
})
```

### Deliverables
- Enhanced `ThinkingChatClient` with context support
- Convenience extension methods (ChatAsync, SendAsync, WithSession)
- DI integration for context services

### Deferred to Later Versions
- Streaming with context support → v0.10.0+ (requires careful design)
- OpenTelemetry integration → v0.11.0 (Observability phase)

---

## v0.10.0 - IDistributedCache Integration ✅

**Goal**: Enable distributed deployment via .NET standard abstractions.

### Critical Insight
Original plan called for separate `RedisThinkingStateStore` and `PostgresThinkingStateStore`.
Research revealed this violates .NET ecosystem conventions:
- `IDistributedCache` already supports Redis, SQL Server, NCache, etc.
- Separate implementations would duplicate existing infrastructure
- "Distributed locking" was unnecessary - state is session-scoped and ephemeral

**Decision**: Wrap `IDistributedCache` instead of creating parallel implementations.

### Completed Tasks
- [x] Implement `DistributedCacheThinkingStateStore` (wraps any IDistributedCache)
- [x] Add `DistributedCacheStateStoreOptions` with expiration controls
- [x] Add `ThinkingStateStoreHealthCheck` (IHealthCheck)
- [x] Add DI extensions: `AddIndexThinkingDistributedStorage()`, `AddIndexThinkingHealthChecks()`

### Usage Example
```csharp
// Use any IDistributedCache backend (Redis, SQL Server, etc.)
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "localhost:6379";
});

services.AddIndexThinkingDistributedStorage(options =>
{
    options.KeyPrefix = "thinking:";
    options.AbsoluteExpiration = TimeSpan.FromHours(1);
});

// Health checks
services.AddIndexThinkingHealthChecks();
services.AddHealthChecks()
    .AddCheck<ThinkingStateStoreHealthCheck>("thinking-store");
```

### Deliverables
- `DistributedCacheThinkingStateStore` - IDistributedCache wrapper
- `ThinkingStateStoreHealthCheck` - IHealthCheck implementation
- DI registration helpers

### Deferred to Later Versions
- PostgreSQL direct implementation → Only if IDistributedCache insufficient
- Distributed locking → Not needed for session-scoped state

---

## v0.11.0 - Observability Integration ✅

**Goal**: IndexThinking-specific telemetry that complements M.E.AI observability.

### Critical Insight

Original plan called for Polly resilience, custom logging, and full metrics/tracing.
Research revealed:
- `Microsoft.Extensions.AI` already provides `UseOpenTelemetry()` and `UseLogging()`
- Polly resilience belongs at `HttpClient` level, not chat client level
- We should only add **IndexThinking-specific** telemetry data

**Decision**: Add Activity tags and Meter for IndexThinking metrics, defer resilience.

### Completed Tasks
- [x] Add Activity tags to ThinkingChatClient (integrates with UseOpenTelemetry):
  - `indexthinking.thinking_tokens`
  - `indexthinking.continuation_count`
  - `indexthinking.truncation_detected`
  - `indexthinking.duration_ms`
  - `indexthinking.complexity`
  - `indexthinking.has_thinking_content`
- [x] Create `IndexThinkingMeter` with .NET Meter API:
  - `indexthinking.turns.total` (Counter)
  - `indexthinking.continuations.total` (Counter)
  - `indexthinking.truncations.total` (Counter)
  - `indexthinking.tokens.thinking` (Counter)
  - `indexthinking.turn.duration` (Histogram)
- [x] Add DI extension: `AddIndexThinkingMetrics()`
- [x] Unit tests

### Usage Example
```csharp
// Recommended pipeline with full observability
var client = new ChatClientBuilder(innerClient)
    .UseOpenTelemetry()          // M.E.AI: GenAI semantic conventions
    .UseLogging(loggerFactory)   // M.E.AI: ILogger integration
    .UseIndexThinking()          // IndexThinking: adds custom tags/metrics
    .Build();

// Configure OpenTelemetry to collect IndexThinking metrics
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter("Microsoft.Extensions.AI")  // M.E.AI metrics
        .AddMeter("IndexThinking"));           // IndexThinking metrics
```

### Deliverables
- `IndexThinkingMeter` in `IndexThinking.Diagnostics` namespace
- Activity tags in `ThinkingChatClient`
- DI registration helper

### Deferred to Later Versions
- Polly resilience → HttpClient/provider level (out of IndexThinking scope)
- Custom logging wrapper → Use M.E.AI's UseLogging() instead
- Grafana dashboard templates → Samples phase (v0.12.0)

---

## v0.12.0 - Sample Applications ✅

**Goal**: Provide working sample applications demonstrating IndexThinking usage.

### Critical Insight

Original plan included Blazor, Docker Compose, and documentation site.
After review:
- Blazor requires significant frontend complexity → Deferred
- Docker Compose is infrastructure concern → Deferred
- Documentation site is separate effort → Deferred

**Decision**: Focus on Console + Minimal API samples that demonstrate core features.

### Environment Configuration

Samples use `.env` file for API keys (GPUStack, OpenAI, Anthropic, Google):
```bash
GPUSTACK_URL=http://...
GPUSTACK_APIKEY=...
OPENAI_API_KEY=sk-...
ANTHROPIC_API_KEY=sk-ant-...
GOOGLE_API_KEY=AIza...
```

### Completed Tasks

#### Console Chat Sample
- [x] Multi-provider support (GPUStack, OpenAI)
- [x] Thinking content visualization (colored output)
- [x] Multi-turn conversation with context
- [x] Token usage display (input, thinking, output)
- [x] Session-based conversation tracking

#### Minimal API Sample
- [x] POST /api/chat endpoint with IndexThinking pipeline
- [x] GET /health endpoint
- [x] Conversation history support
- [x] Structured response (ThinkingInfo, MetricsInfo)

### Deliverables
| Sample | Location | Description |
|--------|----------|-------------|
| Console App | `samples/IndexThinking.Samples.Console/` | CLI chat demo |
| Web API | `samples/IndexThinking.Samples.WebApi/` | Minimal API demo |

### Deferred to v0.13.0+
- Blazor Interactive Demo
- Docker Compose
- Documentation site
- Swagger/OpenAPI documentation

---

## v0.13.0 - Production Hardening

**Goal**: CI/CD automation, integration tests, and NuGet package preparation.

### Critical Insight

Original plan included E2E tests with real LLM providers, performance benchmarking, and documentation site.
After review:
- Real LLM E2E tests require API keys in CI, incur costs → Deferred
- Performance benchmarking requires BenchmarkDotNet setup → Deferred
- Documentation site is significant separate effort → Deferred

**Decision**: Focus on CI automation, mock-based integration tests, and NuGet metadata.

### Tasks

#### GitHub Actions CI
- [ ] Build and test workflow (`.github/workflows/ci.yml`)
- [ ] Multi-platform matrix (ubuntu, windows)
- [ ] PR validation with test results
- [ ] Code coverage reporting

#### Integration Tests
- [ ] Mock `IChatClient` for full pipeline testing
- [ ] `ThinkingChatClient` integration scenarios
- [ ] Context tracking integration
- [ ] State storage integration

#### NuGet Package Metadata
- [ ] `PackageLicenseExpression` (MIT)
- [ ] `PackageReadmeFile` (README.md)
- [ ] `VersionPrefix` for SemVer
- [ ] Source Link configuration

### Deliverables
| Item | Location | Description |
|------|----------|-------------|
| CI Workflow | `.github/workflows/ci.yml` | Automated build/test |
| Integration Tests | `tests/IndexThinking.IntegrationTests/` | Full pipeline tests |
| Package Metadata | `Directory.Build.props` | NuGet publication ready |

### Deferred to v0.15.0+
- Performance benchmarking (BenchmarkDotNet)
- Documentation site
- Real LLM E2E tests (optional, with API keys)

---

## v0.14.0 - Provider-Specific Truncation Handling ✅

**Goal**: Comprehensive handling of all provider-specific finish reasons and stop reasons.

### Critical Insight

Each LLM provider returns different finish/stop reasons for truncation and early termination:
- **OpenAI**: `stop`, `length`, `content_filter`, `tool_calls`, `function_call`
- **Anthropic**: `end_turn`, `max_tokens`, `stop_sequence`, `tool_use`, `refusal`, `model_context_window_exceeded`
- **Google Gemini**: `STOP`, `MAX_TOKENS`, `SAFETY`, `RECITATION`
- **GPUStack/vLLM**: OpenAI-compatible (`stop`, `length`)

**Decision**: Extend TruncationDetector and TruncationReason to handle all known provider-specific reasons.

### Completed Tasks

#### TruncationReason Enum Extension
- [x] `ContentFiltered` - OpenAI `content_filter`, Google `SAFETY`
- [x] `Recitation` - Google `RECITATION` (copyright concerns)
- [x] `Refusal` - Anthropic `refusal` (safety refusal)
- [x] `ContextWindowExceeded` - Anthropic `model_context_window_exceeded`

#### TruncationDetector Enhancement
- [x] ChatFinishReason.Length handling (standard abstraction)
- [x] ChatFinishReason.ContentFilter handling
- [x] Provider-specific stop reason string matching:
  - `max_tokens`, `MAX_TOKENS` → TokenLimit
  - `model_context_window_exceeded` → ContextWindowExceeded
  - `content_filter`, `SAFETY` → ContentFiltered
  - `RECITATION` → Recitation
  - `refusal` → Refusal
- [x] Comprehensive XML documentation for all provider mappings

#### Simulation Tests
- [x] Truncation handling tests for all 4 providers (OpenAI, Anthropic, Google, GPUStack)
- [x] Low max_tokens forced truncation tests
- [x] FinishReason validation tests

### Test Results
- 674 unit tests passing (663 previous + 11 truncation tests)
- 37 simulation tests passing across all providers

### Deliverables
| Item | Location | Description |
|------|----------|-------------|
| TruncationReason enum | `src/IndexThinking/Core/TruncationInfo.cs` | Extended with provider reasons |
| TruncationDetector | `src/IndexThinking/Continuation/TruncationDetector.cs` | Provider-specific handling |
| Simulation Tests | `tests/IndexThinking.SimulationTests/TruncationHandlingTests.cs` | Multi-provider tests |

---

## Dependency Graph

```
v0.1.0 Foundation
    │
    ├── v0.2.0 Token Management
    │       │
    │       └── v0.3.0 Reasoning Parsers I ──┐
    │               │                        │
    │               └── v0.4.0 Truncation ───┤
    │                       │                │
    │                       └── v0.5.0 Storage I
    │                               │
    ├───────────────────────────────┴── v0.6.0 Reasoning Parsers II
    │                                       │
    │                                       └── v0.7.0 Agent Framework
    │                                               │
    │                                               ├── v0.8.0 Memory Integration
    │                                               │
    │                                               └── v0.9.0 SDK & Public API
    │                                                       │
    │                                                       ├── v0.10.0 Storage II
    │                                                       │
    │                                                       └── v0.11.0 Resilience
    │                                                               │
    │                                                               └── v0.12.0 Samples & Demo
    │                                                                       │
    └───────────────────────────────────────────────────────────────────────┴── v0.13.0 Production Hardening
```

---

## Development Workflow

### Test-First Approach
1. Write failing tests for the feature
2. Implement minimum code to pass tests
3. Refactor for clarity and performance
4. Run full test suite
5. Commit with descriptive message

### Commit Convention
```
<type>(<scope>): <subject>

Types: feat, fix, docs, test, refactor, chore
Scope: core, parsers, storage, agents, sdk, samples
```

### Branch Strategy
- `main` - stable releases
- `develop` - integration branch
- `feature/v0.x.0-*` - feature branches

---

## Current Phase Status

### v0.1.0 Foundation - COMPLETE ✅

**Completed:**
- [x] Project structure with Directory.Build.props, Directory.Packages.props
- [x] Core interfaces: IReasoningParser, IThinkingStateStore, ITruncationDetector, ITokenCounter
- [x] Core records: ThinkingContent, ReasoningState, ThinkingState, BudgetConfig, TruncationInfo
- [x] InMemoryThinkingStateStore implementation
- [x] 34 unit tests passing
- [x] nuget.config for package sources
- [x] Solution structure with test projects

### v0.2.0 Token Management - COMPLETE ✅

**Completed:**
- [x] ModelEncodingRegistry for model-to-encoding mapping
- [x] TiktokenTokenCounter with Microsoft.ML.Tokenizers (o200k_base, cl100k_base)
- [x] ApproximateTokenCounter with language-aware character ratio fallback
- [x] TokenCounterChain for fallback strategy pattern
- [x] TokenCounterFactory for counter creation based on model
- [x] 141 unit tests passing (34 foundation + 107 tokenization)

### v0.3.0 Reasoning Parsers I - COMPLETE ✅

**Completed:**
- [x] OpenAI parser models (OpenAIReasoningItem, OpenAIReasoningSummary)
- [x] Anthropic parser models (AnthropicThinkingBlock, AnthropicRedactedThinkingBlock)
- [x] OpenAIReasoningParser for Responses API with encrypted_content support
- [x] AnthropicReasoningParser for Extended Thinking with signature preservation
- [x] ReasoningParserRegistry for provider/model to parser mapping
- [x] 212 unit tests passing (141 previous + 71 parsers)

### v0.4.0 Truncation Handling - COMPLETE ✅

**Completed:**
- [x] TruncationDetector with multi-strategy detection:
  - Primary: ChatFinishReason.Length / provider-specific stop reasons
  - Secondary: Unbalanced braces/brackets, incomplete code blocks (state-based)
  - Tertiary: Mid-sentence endings (heuristic with min length threshold)
- [x] ContentRecoveryUtils for truncated content recovery:
  - JSON repair (add missing closures)
  - Code block closure
  - Clean truncation point detection
  - Fragment combining
- [x] ContinuationConfig for continuation behavior configuration
- [x] 283 unit tests passing (212 previous + 71 continuation)

### v0.5.0 State Storage I - COMPLETE ✅

**Completed:**
- [x] Enhanced InMemoryThinkingStateStore with TTL support:
  - Sliding/absolute expiration modes
  - Optional cleanup timer for expired entries
  - InMemoryStateStoreOptions configuration record
- [x] Implemented SqliteThinkingStateStore for persistent storage:
  - WAL mode for improved concurrency
  - Proper connection management for in-memory vs file-based DBs
  - SqliteStateStoreOptions with factory methods
- [x] Created ThinkingStateSerializer for JSON serialization:
  - snake_case naming policy for API compatibility
  - Base64 encoding for byte[] data (ReasoningState.Data)
  - Custom converters for DateTimeOffset
- [x] Added DI extensions (ServiceCollectionExtensions):
  - AddIndexThinkingInMemoryStorage with options patterns
  - AddIndexThinkingSqliteStorage with connection string support
  - TryAddSingleton for conflict resolution
- [x] 357 unit tests passing (283 previous + 74 storage)

### v0.6.0 Reasoning Parsers II - COMPLETE ✅

**Completed:**
- [x] GeminiReasoningParser with `thoughtSignature` handling:
  - Native Gemini API format (`thoughtSignature` in content parts)
  - OpenAI-compatible format (`extra_content.google.thought_signature`)
  - Thought content extraction and combination
  - Thinking token count extraction
- [x] OpenSourceReasoningParser for DeepSeek/Qwen/vLLM:
  - Structured `reasoning_content` field parsing
  - `<think>...</think>` tag extraction from content
  - vLLM `reasoning` field support
  - Configurable think tag delimiters
- [x] Updated ReasoningParserRegistry with new parsers:
  - Gemini model prefixes (gemini, models/gemini)
  - OpenSource model prefixes (deepseek, qwen, qwq, glm)
- [x] 418 unit tests passing (357 previous + 61 new parsers)

### v0.7.0 Internal Thinking Agents - COMPLETE ✅

**Completed:**
- [x] Core types: `ThinkingContext`, `TurnResult`, `TurnMetrics`, `BudgetUsage`
- [x] `IComplexityEstimator` / `HeuristicComplexityEstimator` - TALE-inspired budget recommendation
- [x] `IContinuationHandler` / `DefaultContinuationHandler` - State machine continuation loop
- [x] `IBudgetTracker` / `DefaultBudgetTracker` - Advisory token tracking
- [x] `IThinkingTurnManager` / `DefaultThinkingTurnManager` - Turn orchestration
- [x] DI extensions: `AddIndexThinkingAgents()`
- [x] 504 unit tests passing (418 previous + 86 agents)

### v0.7.5 ThinkingChatClient - COMPLETE ✅

**Completed:**
- [x] `ThinkingChatClient : DelegatingChatClient` - Main integration point
- [x] `ThinkingChatClientOptions` - Zero-config with customization support
- [x] `UseIndexThinking()` extension for `ChatClientBuilder`
- [x] Response metadata extensions: `GetThinkingContent()`, `GetTurnMetrics()`, `GetTurnResult()`
- [x] 523 unit tests passing (504 previous + 19 client)

### v0.8.0 Memory Integration - COMPLETE ✅

**Scope**: Memory provider abstraction layer for optional Memory-Indexer integration

**Completed**:
- [x] `IMemoryProvider` interface - Abstraction for memory recall
- [x] `MemoryRecallContext` / `MemoryEntry` - Memory recall result types
- [x] `NullMemoryProvider` - No-op default (zero-config mode)
- [x] `FuncMemoryProvider` - Delegate-based integration
- [x] `ThinkingContext.MemoryContext` - Memory context extension
- [x] DI Extensions: `AddIndexThinkingMemory()`, `AddIndexThinkingNullMemory()`
- [x] 559 unit tests passing (523 previous + 36 memory)

**Key Design Decisions**:
- Memory-Indexer = Long-term Memory (past knowledge)
- IndexThinking = Working Memory (current reasoning)
- Integration is optional (NullMemoryProvider is default)
- FuncMemoryProvider enables Memory-Indexer integration without direct dependency
- Fact extraction → Deferred to v0.9.x (separate concern)
- Memory-aware context building → Part of v0.8.5 Query Enhancement

### v0.8.5 Context-Aware Chat Infrastructure - COMPLETE ✅

**Scope**: Conversation context tracking and injection for natural LLM understanding

**The Problem**:
```
LLM API (stateless):              User sends (contextual):
─────────────────────             ─────────────────────────
{ "messages": [...] }             "Do that thing again"
                                  "Make this faster"
                                  "그 파일 수정해줘"
```

**Solution Approach** (after critical review):
- Instead of programmatic pronoun resolution (extra LLM calls, limited accuracy)
- Inject conversation history → LLM naturally understands context

**Completed**:
- [x] `IContextTracker` interface - Session-based conversation tracking
- [x] `InMemoryContextTracker` - Sliding window with TTL support
- [x] `IContextInjector` interface - Context injection abstraction
- [x] `DefaultContextInjector` - Prepends history as messages
- [x] `ConversationTurn` / `ConversationContext` - Core types
- [x] `ContextTrackerOptions` / `ContextInjectorOptions` - Configuration
- [x] DI Extensions: `AddIndexThinkingContext()`
- [x] 65+ unit tests passing (559 previous + new Context tests)

**Key Design Decisions**:
- Zero additional LLM calls (LLM handles context naturally)
- Sliding window pattern (like LangChain/LangChain4j)
- Configurable injection limits for token management
- Thread-safe implementation with cleanup timer

### v0.9.0 Context-Integrated Client - COMPLETE ✅

**Scope**: Integrate conversation context into ThinkingChatClient

**Completed**:
- [x] Integrate IContextTracker/IContextInjector into ThinkingChatClient
- [x] Add context options to ThinkingChatClientOptions
- [x] Update UseIndexThinking() to resolve context services from DI
- [x] Add convenience methods (ChatAsync, SendAsync, WithSession)
- [x] 606 unit tests passing (559 previous + 47 context integration)

### v0.10.0 IDistributedCache Integration - COMPLETE ✅

**Scope**: Distributed deployment via .NET standard abstractions

**Completed**:
- [x] `DistributedCacheThinkingStateStore` wrapping any IDistributedCache
- [x] `DistributedCacheStateStoreOptions` with expiration controls
- [x] `ThinkingStateStoreHealthCheck` (IHealthCheck implementation)
- [x] DI extensions: `AddIndexThinkingDistributedStorage()`, `AddIndexThinkingHealthChecks()`
- [x] 650 unit tests passing (606 previous + 44 distributed storage)

### v0.11.0 Observability Integration - COMPLETE ✅

**Scope**: IndexThinking-specific telemetry complementing M.E.AI

**Completed**:
- [x] Activity tags in ThinkingChatClient for OpenTelemetry integration
- [x] `IndexThinkingMeter` with .NET Meter API
- [x] DI extension: `AddIndexThinkingMetrics()`
- [x] 663 unit tests passing (650 previous + 13 observability)

### v0.12.0 Sample Applications - COMPLETE ✅

**Scope**: Working sample applications demonstrating IndexThinking usage

**Completed**:
- [x] Console Sample (`samples/IndexThinking.Samples.Console/`):
  - Multi-provider support (GPUStack, OpenAI)
  - Thinking content visualization (colored output)
  - Multi-turn conversation with session tracking
  - Token usage display (input, thinking, output)
- [x] Web API Sample (`samples/IndexThinking.Samples.WebApi/`):
  - POST /api/chat endpoint with IndexThinking pipeline
  - GET /health endpoint
  - Conversation history support
  - Structured response (ThinkingInfo, MetricsInfo)
- [x] Solution build succeeds with all samples

**Key Design Decisions**:
- DotNetEnv for `.env` file loading
- ChatClientBuilder pattern with `.UseIndexThinking().Build(sp)`
- Environment variable based provider selection
- Zero dependency on specific LLM providers (GPUStack/OpenAI compatible)

### v0.13.0 Production Hardening - COMPLETE ✅

**Scope**: CI/CD automation, integration tests, and NuGet package preparation

**Completed**:
- [x] GitHub Actions CI workflow (`.github/workflows/ci.yml`)
- [x] Integration tests with mock `IChatClient`:
  - ThinkingChatClient pipeline tests
  - Context tracking integration tests
  - State storage integration tests
  - DI container integration tests
- [x] NuGet package metadata in `Directory.Build.props`
- [x] Source Link configuration for debugging

### v0.14.0 Provider Truncation - COMPLETE ✅

**Scope**: Provider-specific finish reason and truncation handling

**Completed**:
- [x] Extended `TruncationReason` enum:
  - `ContentFiltered` (OpenAI content_filter, Google SAFETY)
  - `Recitation` (Google RECITATION)
  - `Refusal` (Anthropic refusal)
  - `ContextWindowExceeded` (Anthropic model_context_window_exceeded)
- [x] Enhanced `TruncationDetector` with provider-specific handling
- [x] Simulation tests for all 4 providers (OpenAI, Anthropic, Google, GPUStack)
- [x] 674 unit tests + 37 simulation tests passing

### v0.15.0 Reasoning Request Modifiers - COMPLETE ✅

**Scope**: Explicit reasoning activation for providers that require it (DeepSeek, vLLM, GPUStack, Qwen)

### v0.16.0 Streaming Orchestration - COMPLETE ✅

**Scope**: Streaming responses with full thinking orchestration support

**The Problem**:
`GetStreamingResponseAsync` previously bypassed thinking orchestration entirely — no continuation detection, no budget tracking, no reasoning parsing.

**Solution**: Collect-and-Yield pattern (following Microsoft.Extensions.AI's OpenTelemetryChatClient precedent):
1. Stream chunks to caller immediately via `yield return`
2. Buffer all `ChatResponseUpdate` chunks in `List<ChatResponseUpdate>`
3. After stream completes, aggregate via `ToChatResponse()`
4. Process aggregated response through full `IThinkingTurnManager.ProcessTurnAsync()` pipeline
5. Yield final metadata update with `TurnResult`, `TurnMetrics`, `ThinkingContent`

**Completed**:
- [x] Collect-and-Yield streaming in `ThinkingChatClient.GetStreamingResponseAsync`
- [x] Post-stream orchestration: reasoning parsing, budget tracking, metrics collection
- [x] Context injection and conversation tracking for streaming
- [x] Session ID support for streaming requests
- [x] Telemetry tags and meter recording for streaming turns
- [x] Final metadata update with `TurnResult` in `AdditionalProperties`
- [x] Integration tests for streaming orchestration (6 tests)
- [x] Updated unit tests for new streaming behavior
- [x] README updated with streaming usage example
- [x] 741 unit tests + 52 integration tests passing

**Key Design Decisions**:
- Follows the same pattern as Microsoft's `OpenTelemetryChatClient` and `CachingChatClient`
- No breaking changes — existing streaming consumers continue to work
- Metadata is appended as a final update, not mixed into content chunks
- Continuation detection occurs post-stream (not per-chunk), matching current architecture

---

## Version Policy (Detailed)

- **Minor versions** (0.x.0): Feature additions, may include breaking changes
- **Patch versions** (0.x.y): Bug fixes, improvements, no breaking changes
- **Major version** (1.0.0): 커뮤니티 검증 완료 후 수동 승격 (로드맵에서 계획하지 않음)

> Note: 1.0.0 승격은 실제 프로덕션 사용 사례와 커뮤니티 피드백을 통해 API 안정성이 검증된 후에만 진행됩니다.

---

## References

### Official Documentation
- [Microsoft.Extensions.AI](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai)
- [IChatClient Interface](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.ichatclient)
- [xUnit.net v3](https://xunit.net/docs/getting-started/v3/whats-new)

### Research Papers
- TALE: Token-Budget-Aware LLM Reasoning
- SelfBudgeter: Adaptive Token Allocation
- The Illusion of Thinking (Apple ML Research)
