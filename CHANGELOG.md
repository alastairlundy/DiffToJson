# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Removed

- Removed the obsolete hardcoded `ReasoningEffortMatrix` (marked `[Obsolete]` and superseded by the models.dev-backed `ModelsDevReasoningEffortMatrix` in 0.6.0) and its test class. Supported reasoning values are now derived entirely from catalog capability data, plus the small intentional exceptions-only auto table (MiniMax M series, `deepseek-chat`).

### Changed

- `README.md` reasoning effort section rewritten to describe catalog-derived, option-type-driven validation instead of the old hardcoded per-model family lists.
- Reasoning effort wire mapping for Effort-type models is now documented and covered by tests: on OpenAI-style providers `auto`/`on` map to `medium` and `off` sends an explicit `none`; on Anthropic Effort models `auto`/`on` omit the property and `off` sends `reasoning_effort: off`.

### Fixed

- Prompt placeholder validation no longer rejects every built-in preset. `RegexOptions.ExplicitCapture` had made the regex's unnamed capture group non-capturing, so extracted placeholder names were always empty (`unknown placeholder '{}'`), the `PromptPresets` type initializer threw, and any code path touching prompt presets failed with `TypeInitializationException`. The pattern now uses a named capture group.


## [0.7.0] - 2026-10-15

### Added

- New extraction run pipeline: library-side contracts (`IExtractionRunPipeline`, `RunInputs`, `RunProgress`) plus a CLI-side `CliExtractionRunPipeline` implementation, wired into DI with a thin CLI adapter delegating to it.
- Explicit `ExitCode` mapping for pipeline outcomes and cancellation.
- Reasoning effort matrix entries exposed through `IReasoningEffortMatrix` / `ReasoningEffortMatrix`.
- Prompt placeholder vocabulary widened to five placeholders.
- New `DiffToJsonCli.Tests` project covering pipeline precedence, gates, fallback, effort, dispatch, phases, facts, cancellation and exit codes; added to the solution and CI workflow.

### Changed

- Updated CliInvoke to 3.0.3 (3.0.0 is affected by [GHSA-h4rx-7jm8-fr3g](https://github.com/alastairlundy/CliInvoke/security/advisories/GHSA-h4rx-7jm8-fr3g)), but DiffToJson never invokes a runner, so the vulnerable code path is unreachable
- `Program.cs` substantially reworked to delegate to the extraction run pipeline; license file discovery moved out of the CLI helpers.
- Dependabot configuration: schedules tuned and a version cooldown added.
- **Runtime dependencies**: `CliInvoke` and `CliInvoke.Core` 3.0.0 to 3.0.3; `Anthropic` 12.48.0 to 12.57.0; `Microsoft.Extensions.AI.Abstractions`, `Microsoft.Extensions.AI.OpenAI` and `Microsoft.Extensions.Compliance.Redaction` 10.10.0 to 10.11.0; `Microsoft.Extensions.DependencyInjection` 10.0.12 to 10.0.13; `OllamaSharp` 5.4.30 to 5.5.0; `Kevlar` 1.1.1 to 1.3.0; `System.CommandLine` 2.0.12 to 2.0.13.
- **Testing dependencies**: `TUnit` 1.68.4 to 1.75.0.
- `GLOSSARY.md` updated with new pipeline terminology.

### Fixed

- `GitLogParser` output is now platform-independent, and golden file comparison normalizes line endings across OSes.
- Code review findings addressed across the extraction run pipeline.

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

[Unreleased]: https://github.com/alastairlundy/DiffToJson/compare/0.7.0...HEAD
[0.7.0]: https://github.com/alastairlundy/DiffToJson/compare/0.6.0...0.7.0
[0.6.0]: https://github.com/alastairlundy/DiffToJson/compare/0.5.1...0.6.0
