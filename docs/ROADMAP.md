# IndexThinking Roadmap

> v0.1.0 ~ v1.0.0-beta Development Phases

This roadmap outlines the incremental development strategy for IndexThinking, a Working Memory manager for Reasoning-capable LLMs.

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
| **v0.8.5** | **Query Enhancement** | **Context tracking, ambiguous query resolution, memory integration** |
| **v0.9.0** | **Session-Aware SDK** | **IThinkingService, userId/sessionId API** |
| v0.10.0 | State Storage II | Redis + PostgreSQL distributed stores |
| v0.11.0 | Resilience & Observability | Polly integration, telemetry, logging |
| v0.12.0 | Samples & Demo | Console, Web API, Blazor applications |
| v1.0.0-beta | Production Ready | E2E testing, documentation, performance tuning |

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

- [ ] `dotnet build IndexThinking.sln` succeeds with no warnings
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

- [ ] `dotnet build IndexThinking.sln` succeeds with no warnings
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

## v0.8.5 - Query Enhancement

**Goal**: Transform ambiguous user messages into clear, context-aware prompts.

### Motivation

LLM APIs are stateless, but users speak in context:
```
User sends:     "Do that thing again"
LLM needs:      "Python DataFrame을 CSV로 저장하는 작업을 다시 실행해주세요"
```

IndexThinking bridges this gap by:
1. Tracking conversation context (recent topics, entities)
2. Resolving ambiguous references ("this", "that thing", "make it faster")
3. Enriching queries with relevant memory (via v0.8.0 IMemoryProvider)

### Core Interfaces

```csharp
/// <summary>
/// Tracks conversation context within a session.
/// </summary>
public interface IContextTracker
{
    void Track(string sessionId, ChatMessage message, ChatResponse response);
    ConversationContext GetContext(string sessionId);
}

/// <summary>
/// Enriches user messages with context and memory.
/// </summary>
public interface IQueryEnricher
{
    Task<IList<ChatMessage>> EnrichAsync(
        ThinkingContext context,
        IList<ChatMessage> messages,
        CancellationToken ct = default);
}

public record ConversationContext
{
    public IReadOnlyList<string> RecentTopics { get; init; }
    public IReadOnlyDictionary<string, string> Entities { get; init; }  // "그 파일" → "app.py"
    public string? LastAction { get; init; }  // "Python 코드 실행"
}
```

### Tasks
- [ ] Define `IContextTracker` interface
- [ ] Implement `InMemoryContextTracker`
- [ ] Define `IQueryEnricher` interface
- [ ] Implement `DefaultQueryEnricher`
  - Pronoun resolution ("this" → actual entity)
  - Action reference ("that thing" → last action)
  - Topic continuation detection
- [ ] Define `IFollowUpGenerator` interface
- [ ] Implement `DefaultFollowUpGenerator`
  - Context-aware follow-up question suggestions
  - Response-based next action recommendations
- [ ] Integrate with `IMemoryProvider` (v0.8.0)
- [ ] Add to ThinkingChatClient pipeline (pre + post processing)

### Test Requirements
- [ ] Pronoun resolution tests
- [ ] Action reference resolution tests
- [ ] Topic detection tests
- [ ] Integration with memory recall

### Configuration

```csharp
.UseIndexThinking(options =>
{
    options.EnableQueryEnrichment = true;     // Enable pre-processing
    options.ContextWindowSize = 5;            // Track last 5 turns
    options.ResolvePronouns = true;           // "this" → entity
    options.ResolveActionReferences = true;   // "that thing" → action
})
```

### Deliverables
- `IndexThinking.Context` namespace
- Query enrichment pipeline in ThinkingChatClient

---

## v0.9.0 - SDK & Public API (Session-Aware)

**Goal**: Create developer-friendly, session-aware SDK that eliminates chat app boilerplate.

### Vision

```csharp
// What developers want to write:
var response = await thinking.ChatAsync(
    userId: "user-123",
    sessionId: "session-456",
    message: "Do that thing again"
);

// IndexThinking handles everything:
// - Session history loading
// - Context interpretation ("that thing" → actual meaning)
// - Prompt optimization
// - LLM request with truncation handling
// - Response parsing and state saving
```

### Core Interface

```csharp
public interface IThinkingService
{
    /// <summary>
    /// Session-aware chat that handles all complexity.
    /// </summary>
    Task<ThinkingResponse> ChatAsync(
        string userId,
        string sessionId,
        string message,
        ChatOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Streaming version.
    /// </summary>
    IAsyncEnumerable<ThinkingResponseUpdate> ChatStreamingAsync(...);
}
```

### Tasks
- [ ] Design `IThinkingService` facade
- [ ] Implement session loading/saving
- [ ] Integrate query enhancement (v0.8.5)
- [ ] Integrate memory recall (v0.8.0)
- [ ] Add streaming support
- [ ] Add OpenTelemetry activity support
- [ ] Create DI extension methods

### Test Requirements
- [ ] End-to-end session tests
- [ ] Streaming tests
- [ ] Multi-user isolation tests

### Deliverables
- `IndexThinking.SDK` with `IThinkingService`
- Session-aware API

---

## v0.10.0 - State Storage II (Distributed)

**Goal**: Enable distributed deployment.

### Tasks
- [ ] Implement `RedisThinkingStateStore`
- [ ] Implement `PostgresThinkingStateStore`
- [ ] Add distributed locking
- [ ] Add health checks

### Test Requirements
- [ ] Integration tests with containerized Redis/PostgreSQL

### Deliverables
- `IndexThinking.Storage.Redis`
- `IndexThinking.Storage.PostgreSQL`

---

## v0.11.0 - Resilience & Observability

**Goal**: Production-ready resilience and observability.

### Tasks
- [ ] Integrate Polly for resilience
- [ ] Implement retry strategies
- [ ] Add comprehensive logging
- [ ] Implement metrics
- [ ] Add tracing

### Test Requirements
- [ ] Retry behavior tests
- [ ] Circuit breaker tests

### Deliverables
- `IndexThinking.Resilience` namespace
- Grafana dashboard templates

---

## v0.12.0 - Samples & Demo

**Goal**: Provide comprehensive sample applications for developers.

### Tasks

#### Console Chat Application
- [ ] Simple CLI chat with thinking visualization
- [ ] Multi-turn conversation demo
- [ ] Continuation handling demo
- [ ] Budget management demo

#### ASP.NET Core Web API
- [ ] REST API with streaming support
- [ ] Swagger/OpenAPI documentation
- [ ] Health check endpoints
- [ ] Rate limiting example

#### Blazor Interactive Demo
- [ ] Real-time thinking visualization
- [ ] Token budget visualization
- [ ] Provider comparison demo
- [ ] State inspection UI

#### Documentation
- [ ] Getting started guide
- [ ] Provider-specific setup guides
- [ ] Best practices guide
- [ ] Troubleshooting guide

### Test Requirements
- [ ] Sample applications compile and run
- [ ] API endpoints return expected responses
- [ ] Blazor UI renders correctly

### Deliverables
| Sample | Location | Description |
|--------|----------|-------------|
| Console App | `samples/ConsoleChat/` | CLI demonstration |
| Web API | `samples/WebApi/` | REST API example |
| Blazor Demo | `samples/BlazorDemo/` | Interactive web UI |
| Docker Compose | `samples/docker-compose.yml` | Full stack deployment |

---

## v1.0.0-beta - Production Ready

**Goal**: Comprehensive testing, documentation, and production hardening.

### Tasks
- [ ] End-to-end test suite
- [ ] Performance testing
- [ ] Security review
- [ ] Documentation site
- [ ] CI/CD pipeline
- [ ] Performance optimization

### Deliverables
- Complete test coverage (>80%)
- Published NuGet packages
- Documentation site

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
    └───────────────────────────────────────────────────────────────────────┴── v1.0.0-beta
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

### v0.8.5 Query Enhancement - PLANNED

**Scope**: Transform ambiguous messages into context-aware prompts

**The Problem**:
```
LLM API (stateless):              User sends (contextual):
─────────────────────             ─────────────────────────
{ "messages": [...] }             "Do that thing again"
                                  "Make this faster"
                                  "그 파일 수정해줘"
```

**Components to Implement**:
- [ ] `IContextTracker` - Track recent topics, entities, actions
- [ ] `IQueryEnricher` - Resolve pronouns, action references
- [ ] Pre-processing pipeline in ThinkingChatClient
- [ ] Integration with IMemoryProvider

**Key Design Decisions**:
- Zero-config default (basic context tracking)
- Optional memory integration for richer context
- Pronoun/reference resolution is language-aware

---

## Version Policy

- **Minor versions** (0.x.0): Feature additions, may include breaking changes
- **Patch versions** (0.x.y): Bug fixes, no breaking changes
- **Beta** (1.0.0-beta): Feature complete, API stabilization
- **Stable** (1.0.0): Production ready, semantic versioning begins

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
