# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.6.0] - 2026-09-18

### Global (defaulted)

#### Added
- Added a models.dev-backed reasoning effort matrix. It looks up capabilities per provider and derives support from the option type (Effort, Toggle, BudgetTokens). MiniMax M-series and deepseek-chat stay on a small exceptions-only auto table.
- Added an offline-tolerant cache for models.dev data. It has a 24-hour TTL with stale-serve fallback and AOT-safe deserialization. Runs never block when capability data is unavailable.
- Added source key resolution components (model ID normalization and models.dev provider mapping) for cross-provider capability lookups.
- The CLI now uses the models.dev-backed matrix. When capability data is missing it prints a one-line stderr warning and falls back to auto-only.
- Added property-based fuzz tests (FsCheck) covering Git log parsing, PII redaction, prompt substitution, and commit record serialization.

#### Changed
- Dropped per-model family classification. Dispatch now follows the reasoning option type: Toggle, BudgetTokens, or Effort.
- Migrated process execution from the CliInvoke v2 API to the CliInvoke v3 buffered API.
- Replaced Polly with Kevlar for LLM retry resilience.
- Replaced Verify snapshot assertions with plain TUnit assertions in end-to-end regression tests. Snapshot files are pinned to LF line endings.
- Updated glossary documentation.
- **Runtime Dependencies**: added `ModelsDotDevSharp` 0.2.0 and `Kevlar` 1.1.1; `CliInvoke.Core` 2.11.0 to 3.0.0 (replaced `CliInvoke.Extensions` with `CliInvoke` 3.0.0); `Anthropic` 12.46.0 to 12.48.0; `Microsoft.Extensions.AI.Abstractions` 10.9.0 to 10.10.0; `Microsoft.Extensions.AI.OpenAI` 10.9.0 to 10.10.0; `Microsoft.Extensions.Compliance.Redaction` 10.9.0 to 10.10.0; `Microsoft.Extensions.DependencyInjection` 10.0.11 to 10.0.12; `System.CommandLine` 2.0.11 to 2.0.12.
- **Testing Dependencies**: added `FsCheck` 3.4.0; `TUnit` 1.66.10 to 1.68.4; removed `Verify` and `Verify.TUnit`.

#### Removed
- Removed the `ModelFamily` enum and per-family `ClassifyModel` API. Type-driven dispatch covers it.
- Removed the `Polly` dependency, replaced by `Kevlar`.
- Removed the `Verify` and `Verify.TUnit` snapshot-testing dependencies in favor of plain TUnit assertions.

#### Fixed
- Fixed the Anthropic (old) reasoning-off path to omit the thinking block instead of sending `budget_tokens: 0`. Budget values now clamp to the 1024 minimum Anthropic requires.
- Filled in the `ChatOptionsBuilder` placeholder methods with working builders for OpenAI adaptive, Anthropic-compatible, and MiniMax thinking toggle/adaptive shapes.
- Hardened the capability cache, made startup non-blocking when capability data is missing, and fixed model ID normalization and dispatch.
- Fixed Verify snapshot line-ending failures on Windows.

[0.6.0]: https://github.com/alastairlundy/DiffToJson/compare/0.5.1...0.6.0
