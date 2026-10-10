# Changelog

All notable changes to this project are documented in this file. Versions follow
[Semantic Versioning](https://semver.org/); while the major version is 0, a minor release may contain
breaking changes, and each one is marked **Breaking** with a migration note.

## [Unreleased]

### Changed
- **The packages from this repository depend on each other at exactly the same version** (`[x.y.z]`), not a floor.
  A consumer that moves one of them while another resolves at an older version now gets restore warning NU1608 naming
  the pair (an error where warnings are errors) — before, the mixed versions restored silently and could fail at run time.
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
