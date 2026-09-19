# Changelog

All notable changes to this project are documented in this file. Versions follow
[Semantic Versioning](https://semver.org/); while the major version is 0, a minor release may contain
breaking changes, and each one is marked **Breaking** with a migration note.

## [0.22.0] - 2026-09-19

### Removed

- **Breaking: the budget and option surfaces nothing read.** `BudgetConfig`, `ThinkingChatClientOptions.DefaultBudget`,
  `ThinkingContext.Budget`/`WithBudget`, `IBudgetTracker.IsThinkingBudgetExceeded`/`IsAnswerBudgetExceeded`,
  `IComplexityEstimator.GetRecommendedBudget`, `ThinkingChatClientOptions.AutoEstimateComplexity`, and `AgentOptions`
  with the `configure` parameter of `AddIndexThinkingAgents`. A thinking budget set here was attached to the turn and
  never applied, so it looked like a per-turn cap and was not one; the options registered by `AddIndexThinkingAgents`
  were never read either. Migration: cap reasoning on the request (`ChatOptions.Reasoning`, `ChatOptions.MaxOutputTokens`);
  continuation limits are `ThinkingChatClientOptions.DefaultContinuation`; call `AddIndexThinkingAgents()` without arguments.

## [0.21.2] - 2026-09-10

This file starts at 0.21.2. Changes in earlier releases were not recorded here; the commit history is
the record for them.
