# Changelog

All notable changes to this project are documented in this file. Versions follow
[Semantic Versioning](https://semver.org/); while the major version is 0, a minor release may contain
breaking changes, and each one is marked **Breaking** with a migration note.

## [0.31.0] - 2026-10-18

### Added
- **Token counts fitted to local models' tokenizers: `LanguageRatios.Gemma`, `LanguageRatios.Qwen`, `LanguageRatios.ForModel(modelId)`
  and `ApproximateTokenCounter.ForModel(modelId)`.** The approximate counter is fitted to o200k_base; Gemma's and Qwen's
  tokenizers split digits and punctuation finer, so with the default ratios a JSON tool result counted about a quarter (Gemma 4)
  or a fifth (Qwen 3.5) too low — beyond the 15 % input margin of the output cap, on exactly the large results that fill a
  small local window. With the family ratios the sums are within 5 % on English, Korean, mixed prose, C# and JSON, measured
  against each model's own tokenizer. `ForModel` picks the family from the id (`gemma…`, `…qwen…`, e.g. `gguf:qwen3-fast`);
  any other id keeps the o200k-fitted defaults.

### Changed
- **`TokenCounterFactory.Create(modelId)` uses the model's family ratios** for a Gemma or Qwen id (a factory constructed with
  its own `LanguageRatios` keeps them). Counts for those models rise — most on JSON and code. A host that registers the
  approximate counter itself (`AddIndexThinkingAgents()` registers the default) and serves a local model passes
  `ApproximateTokenCounter.ForModel(modelId)` to `ThinkingChatClient` / `ToolResultMasker` / `ContextBudgetOptions.TokenCounter`.

## [0.30.0] - 2026-10-17

### Added
- **`ToolResultMasker` can wait until masking is worth a prompt-cache miss: `minimumClearTokens`.** Masking changes the
  prompt from the first masked result on, and each new turn moves the protection boundary by one turn — so masking at every
  move makes a provider's prompt cache miss on every turn. With `minimumClearTokens` the masker leaves the history as it is
  until the results it would mask add up to that many tokens, then masks them together (the trade-off Anthropic's context
  editing calls `clear_at_least`). Results already masked do not count. It applies to the turn boundary only — a
  `protectedTokens` budget is a limit and always holds. Default 0: as before. **Breaking** (binary): the constructor gained the parameter,
  so a package compiled against 0.29.0 that constructs a masker needs a rebuild — IronHive.Agent 0.65.0 does; take IronHive.Agent
  0.66.0 with this release.

### Fixed
- **A masked tool result stays masked as it was.** A loop that stores the prepared history hands the masker its own
  placeholders on the next request; a placeholder with long arguments is longer than `minimumResultLength`, and it was masked
  again — the new placeholder described the placeholder («230 chars») instead of the result, and the prompt changed at that
  result once more. Placeholders are now left alone.
- **A trimmed tool result stays trimmed as it was.** `ToolResultTrimmer`'s output can still be over `MaxChars` (the
  character cut adds its marker after the limit; kept head and tail lines can be long), so a loop that stores what it sent
  trimmed it again on the next request and the marker's «chars total» then described the trimmed text. A text that carries
  the trimmer's own marker is now returned as it is.

## [0.29.0] - 2026-10-17

### Added
- **Over the budget, `ContextBudgetChatReducer` masks old tool results before it drops anything: `ToolResultMasker`.** A
  result outside the newest three user turns (or, with `protectedTokens`, past a token budget counted from the newest
  back — the newest round always stays) becomes a placeholder naming the call, its size and how to get it again; the call
  stays, so the model still knows what it read. `ContextBudgetOptions.Masking` (default on, `null` skips it) and
  `ContextBudgetOptions.Retention` — an `IToolResultRetention` keeps each masked original and the placeholder names its id
  and the tool that reads it back instead of asking for the same call (IronHive.Agent's `retrieve_tool_result` is one).
  Messages are dropped only when masking is not enough. The masker is IronHive.Agent's `ObservationMasker` rule.

## [0.28.0] - 2026-10-17

### Added
- **Fit a request into a token budget without breaking tool rounds: `ContextBudgetChatReducer`.** An M.E.AI `IChatReducer`
  (`ChatClientBuilder.UseChatReducer(…)`) for any `IChatClient` pipeline: a tool result over its limit keeps its first and
  last lines (`ContextBudgetOptions.ToolResults`, a `ToolResultTrimmer` — 30 000 characters, 50 + 20 lines; the same rule
  as IronHive.Agent's tool-result compaction), and while the request is over `MaxInputTokens` the oldest messages are dropped — an assistant
  message that calls tools always together with the results that answer it, so no request carries a call without its result
  or a result without its call (M.E.AI's own reducers count messages and can split them). System messages, the last user
  message and the newest message or round stay; each dropped run is replaced by one note where it was. The caller's history
  is untouched. Registered after `UseFunctionInvocation()`, it fits every round of a tool loop.

### Changed
- **Scope: IndexThinking shapes one model call** — it fits what goes in to the window and reads what comes out (was: «one
  LLM turn»). The README and `docs/DESIGN.md` say so; no API changed with it.

## [0.27.0] - 2026-10-16

### Added
- **The client's input estimate learns from the provider: `ThinkingChatClientOptions.CalibrateTokenCounts` (default on).**
  Each response's `Usage.InputTokenCount` is compared with the estimate for the same request, and a moving factor
  (`ThinkingChatClient.TokenEstimateFactor`, primitive `TokenEstimateCalibration`) scales later estimates for the context
  check and the output cap — a model whose tokenizer is not o200k (most local models, most on Korean) is no longer
  undercounted by a steady factor. Requests under 256 estimated tokens are not observed; the factor stays within 0.75–3.

### Changed
- **Breaking: a message's token count covers everything it sends, and the approximate estimate matches o200k.** Before,
  `Count(ChatMessage)` (approximate and exact alike) counted only `TextContent`: a tool result or a tool call counted as an
  empty message (estimate/real 0.006), so the output cap and the input check ignored the bulk of an agent's history. Now a
  tool call counts its name and arguments, a tool result its payload (a string, a `JsonElement`'s text, the serialized
  value, or - for a result made of content, such as a screenshot or an MCP tool's text and images - its text and images),
  reasoning text counts, and each image counts `ApproximateTokenCounter.ImageTokens` (1,000). The approximate
  counter now counts letters, digits, punctuation and whitespace runs apart, with ratios fitted to o200k_base: on English chat,
  Korean passages, mixed Korean/English prose, C# and JSON every set's estimate is within 3 % of o200k (before: Korean −25 %,
  JSON −33 %, C# −13 %). Counts grow for most requests, so the same input can now raise `ContextWindowExceededException`
  where it passed, and the output cap is lower. `LanguageRatios.English` now means Latin letters only (7.0, was 4.0 over
  letters and punctuation); new `Digit`, `Punctuation` and `WhitespaceRun` ratios; `Korean` 1.35 (was 1.5). Migration: a
  custom `LanguageRatios` built for the old meaning should be dropped or refitted; a whitespace-only text now counts 1, not 0.
- **Breaking: the context check and the output cap count the request's tool definitions.** Each tool's name, description
  and parameter schema travel with every request (often thousands of tokens for a full tool set) but were not in the
  estimate, so a request with many tools could pass the check and be capped past the window. The check now raises
  `ContextWindowExceededException` and the cap lowers `MaxOutputTokens` accordingly.

### Fixed
- **A comment promised a retry that does not exist.** `CapMaxOutputTokens` said an estimate that still overflows is
  «handled by catch-and-retry»; nothing retries it — such a request fails with the provider's context error.

## [0.26.0] - 2026-10-11

### Removed
- **Breaking: the long-term memory contract is gone** — `IMemoryProvider`, `NullMemoryProvider`, `FuncMemoryProvider`,
  `MemoryRecallHandler`/`MemoryRememberHandler`, `MemoryRecallResult`, `MemoryRecallContext`, `MemoryEntry`, `MemoryScope`,
  `MemoryStoreRequest`, `AddIndexThinkingMemory(…)` (all overloads), `AddIndexThinkingNullMemory()`, and
  `ThinkingContext.UserId`/`MemoryContext`/`WithUserId`/`WithMemory`/`HasMemory`, together with `docs/MEMORY_INTEGRATION.md`.
  The turn pipeline never called the provider, so registering one changed nothing. Migration: delete the registration; to give
  a turn recalled memories, recall them from your memory store before the turn and add
  them to the messages.
- **Breaking: response and request wire models the parsers never read are gone** — `AnthropicMessageResponse`,
  `AnthropicUsage`, `AnthropicThinkingConfig`, `GeminiResponse`, `GeminiCandidate`, `GeminiContent`, `GeminiUsageMetadata`,
  `GeminiThinkingConfig`, `OpenAIReasoningUsage`, `OpenAIOutputTokenDetails`, `OpenAIReasoningConfig`. The parsers read the
  provider JSON themselves and only use the content-part and reasoning-item models, which stay. Migration: declare your own
  type if you serialized one of these.

### Changed
- **The nuget.org «Release Notes» of every package link to the GitHub release of its version**, whose notes are this CHANGELOG's section for that version.

## [0.25.1] - 2026-10-11

### Fixed
- **`AddIndexThinkingMetrics()` and `AddIndexThinkingReasoningModifiers(…)` take effect on a client built with
  `UseIndexThinking()`.** The DI form never resolved the registered `IndexThinkingMeter` or `ReasoningRequestModifierRegistry`,
  so no turn was recorded and a custom registry was ignored. A registered registry still does not turn reasoning on: that
  remains `ThinkingChatClientOptions.EnableReasoning`.

### Documentation
- **`docs/MEMORY_INTEGRATION.md` says that the turn pipeline does not call `IMemoryProvider`.** The guide had presented it as
  integrated. The contract is unchanged; call it from your own code.

### Changed
- **The packages from this repository depend on each other at exactly the same version** (`[x.y.z]`), not a floor.
  A consumer that moves one of them while a family member it depends on resolves at an older version now gets restore warning NU1608 naming
  the pair (an error where warnings are errors) — before, the mixed versions restored silently and could fail at run time.
  A member that reaches you only through another package's floor is not covered: pin every member you load and move them together.
- **Breaking: a cancelled `ThinkingStateStoreHealthCheck` throws `OperationCanceledException` instead of reporting
  `Unhealthy`.** A probe whose caller gave up (for example a health endpoint request that was aborted) said the store was
  not accessible. The check's own `Timeout` still reports `Degraded`. Migration: catch `OperationCanceledException` if you
  call `CheckHealthAsync` directly and relied on the `Unhealthy` result.

## [0.25.0] - 2026-10-06

### Removed
- **Breaking: public types that nothing used are removed.** No code path in this library constructed, returned or accepted them, and no implementation existed where they were interfaces. Code that never named them is unaffected; code that did can delete the reference - there was no behaviour behind it.
  Removed: `OpenSourceChatCompletion`, `OpenSourceChoice`, `OpenSourceUsage`, `OpenSourceTokenDetails`, `OpenSourceModelFamilies`.

## [0.24.2] - 2026-10-05

### Dependencies
- Microsoft.Extensions.AI 10.10.0, Microsoft.Extensions.AI.Abstractions 10.10.1, Microsoft.Extensions.AI.OpenAI 10.10.1 (from 10.6.0); Microsoft.Extensions.* and Microsoft.Data.Sqlite 10.0.12 servicing.

## [0.24.1] - 2026-10-04

### Fixed
- **A continued answer keeps the turn's tool rounds.** When the answer of a turn that had already called tools was cut
  off and continued, the final response held only the combined answer: the tool calls and their results were dropped, so
  the caller's history lost them (the next turn's model no longer saw what its tools did) and a caller counting the
  turn's tool calls saw none. The continuation requests also omitted them. Both now keep the turn's earlier messages; the
  continued text is the final answer alone (not earlier assistant text repeated), and usage sums every request.
- **A continuation request offers no tools.** It carried the caller's tools, so a function-invoking client below ran a
  fresh round of calls on each continuation — past any iteration cap the turn had reached — and those calls were never
  recorded in the turn (only the continued text was kept). A continuation now only finishes the answer.

## [0.24.0] - 2026-10-02

### Fixed
- **A truncated stream is reported as truncated instead of being "continued" with itself.** On the streaming path the
  continuation request returned the same aggregated response, so a stream cut off at the output limit was appended to
  itself up to `MaxContinuations` + 1 times and that repeated text was what the conversation tracker recorded. The
  caller already holds every chunk, so `GetStreamingResponseAsync` now reports the turn as `TurnResult.WasTruncated`
  without continuing it or repairing its JSON/code blocks; what is recorded is what was streamed. Truncation recovery
  (continuation and content repair) remains a non-streaming feature.

### Changed
- **Packages carry the license text.** Each `.nupkg` includes `LICENSE` next to the `MIT` expression.

## [0.23.1] - 2026-10-01

### Dependencies

- `Anthropic` 12.44.0 → 12.53.0.

## [0.23.0] - 2026-09-26

### Changed

- **Breaking: the SQLite state store moved to a new package, `IndexThinking.Sqlite`.** `SqliteThinkingStateStore`,
  `SqliteStateStoreOptions` and `AddIndexThinkingSqliteStorage` keep their names and namespaces; `IndexThinking` no
  longer depends on `Microsoft.Data.Sqlite` or `SQLitePCLRaw.bundle_e_sqlite3`, so a consumer that never persists
  thinking state stops shipping the native `e_sqlite3` (about 2 MB on one RID, one copy per RID in a portable build).
  Migration: add a package reference to `IndexThinking.Sqlite` if you call `AddIndexThinkingSqliteStorage` or use the
  store directly.

## [0.22.1] - 2026-09-23

### Fixed
- **`TokenCounterChain.IsApproximate(modelId)` answers for the counter the chain uses for that model.** It fell through to
  `ITokenCounter`'s default and returned `false` for every model — including the ones the chain routes to its approximate
  fallback (a chain built for `claude-3` counts approximately and said it did not). A model no counter supports is
  reported approximate.

## [0.22.0] - 2026-09-19

### Removed

- **Breaking: the budget and option surfaces nothing read.** `BudgetConfig`, `ThinkingChatClientOptions.DefaultBudget`,
  `ThinkingContext.Budget`/`WithBudget`, `IBudgetTracker.IsThinkingBudgetExceeded`/`IsAnswerBudgetExceeded`,
  `IComplexityEstimator.GetRecommendedBudget`, `ThinkingChatClientOptions.AutoEstimateComplexity`, and `AgentOptions`
  with the `configure` parameter of `AddIndexThinkingAgents`. A thinking budget set here was attached to the turn and
  never applied, so it looked like a per-turn cap and was not one; the options registered by `AddIndexThinkingAgents`
  were never read either. Migration: cap reasoning on the request (`ChatOptions.Reasoning`, `ChatOptions.MaxOutputTokens`);
  continuation limits are `ThinkingChatClientOptions.DefaultContinuation`; call `AddIndexThinkingAgents()` without arguments.
- **Breaking: `ThinkingChatClientOptions.ContextTrackerOptions`, `ContextInjectorOptions` and `MaxContextTurns`.** The
  client takes its tracker and injector as constructor arguments, each built with its own options, so these copies were
  never read; `MaxContextTokens` no longer writes into the dead `ContextInjectorOptions` copy. Migration: pass the options
  where the tracker and injector are built (`AddIndexThinkingContext(trackerOptions, injectorOptions)`).

### Added

- **An options roster test** (`Iyu.Conventions.Testing`): every public option must be read by the library, or be listed
  with its reason.

## [0.21.2] - 2026-09-10

This file starts at 0.21.2. Changes in earlier releases were not recorded here; the commit history is
the record for them.
