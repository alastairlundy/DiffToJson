# Implementation Blueprint — Training Example Pipeline

## Scope Binding

- **Linked Spec**: `docs/decisions/DECISIONS-DiffToJson-training-example-pipeline.md` (D001–D012)
- **Decision Ledger**: `docs/decisions/DECISIONS-DiffToJson-training-example-pipeline.md` (T001–T020)

This blueprint is a **context pointer valid ONLY for the linked spec** (the Decision Ledger above). It synthesizes the resolved T-records into implementation tasks for an implementer who has not read the ledger. Every technical statement in the body that satisfies a functional requirement cites the `Dxxx`/`Txxx` record it satisfies using `filename#<Dxxx|Txxx>` format. The blueprint is a snapshot; once a T-record is added to the ledger, this blueprint is stale and must be re-derived. Re-read the ledger before any blueprint edit.

---

## 1. Goal

Refactor the `DiffToJson` training-example pipeline so the inline `BuildTrainingRecords` in `Program.cs` and its duplicate in `GitCommitParser.ParseCommitsToTrainingStreamAsync` are replaced by a single, testable, library-owned pipeline: a new `TrainingExampleBuilder` in `DiffToJsonLib.Training` consumes a stream of raw `CommitRecord` instances, applies redaction via `RedactionPolicy`, optionally calls an `IAssistantMessageGenerator` (LLM-backed or disabled), and emits `CommitTrainingRecord` instances. The new `GitLogParser` extracts the git-log state machine into a pure, git-free module. The library gains a TUnit test project (`tests/DiffToJsonLib.Tests`) that covers every new and refactored module, with an end-to-end regression test pinned to a `Verify` golden file.

---

## 2. New Module Layout

### Library (`DiffToJsonLib`)

| File | Role | Cites |
|---|---|---|
| `Parsing/GitLogParser.cs` | Pure state machine that splits `git log -p` output into `(message, diff)` pairs via `IAsyncEnumerable<RawCommit> ParseAsync(TextReader, CancellationToken)`. No `git`, no I/O, no redaction. | `DECISIONS-...md#D002`, `#T006` |
| `Training/Abstractions/IAssistantMessageGenerator.cs` | Interface: `Task<AssistantMessageResult> GenerateAsync(string systemPrompt, string userPrompt, CommitRecord redactedCommit, ChatOptions options, CancellationToken ct)`. | `DECISIONS-...md#D001`, `#T011`, `#T015` |
| `Training/AssistantMessageResult.cs` | Sealed record hierarchy: `AssistantMessageGenerated(string Content, string OriginalAssistantMessage)`, `AssistantMessageDisabled(string FallbackContent)`, `AssistantMessageAttemptedAndFailed(string? FallbackContent, string OriginalAssistantMessage)`. Private constructor on the abstract base. | `DECISIONS-...md#D003`, `#T004`, `#T017` |
| `Training/RedactionPolicy.cs` | Singleton: `Redact(CommitRecord, RedactionTier) → CommitRecord` (commit field-scope per tier) and `Redact(string, RedactionTier) → string` (general text). Backed by `IReadOnlyDictionary<RedactionTier, Redactor>`. | `DECISIONS-...md#D007`, `#T008`, `#T016` |
| `Training/PromptSubstitutor.cs` | `public static class` with `Substitute(string template, string diff, string commitMessage, string repoName, string license, string repoUrl)`. No I/O, no validation. | `DECISIONS-...md#D004`, `#T010` |
| `Training/TrainingExampleOptions.cs` | `public sealed record` with `PromptTemplate Template`, `string? LlmOverridePrompt`, `bool LlmAssistantOutput`, `RedactionTier Tier`, `ChatOptions Options`. | `DECISIONS-...md#D004`, `#D005`, `#D007`, `#T005`, `#T015` |
| `Training/TrainingExampleBuilder.cs` | `public sealed class` with constructor `(RedactionPolicy redactor, IAssistantMessageGenerator assistant)`; `BuildAsync(IAsyncEnumerable<CommitRecord>, TrainingExampleOptions, CancellationToken) → IAsyncEnumerable<CommitTrainingRecord>`. Switch expression over `AssistantMessageResult`. | `DECISIONS-...md#D001`, `#D005`, `#D006`, `#T007`, `#T013` |
| `Training/DisabledAssistantMessageGenerator.cs` | Returns `AssistantMessageDisabled(FallbackContent: redactedCommit.CommitMessage)`. | `DECISIONS-...md#D005`, `#T011` |

### Refactored Library

| File | Change | Cites |
|---|---|---|
| `Abstractions/IGitCommitParser.cs` | Shrink from 3 methods to 2: `ParseCommitsToArrayAsync` and `ParseCommitsStreamAsync`. | `DECISIONS-...md#D002`, `#T018` |
| `GitCommitParser.cs` | Thin shell: `GitLogParser` invoked internally; `IProcessInvoker` only constructor dep; raw (unredacted) `CommitRecord` output. The `IRedactorProvider` dep, the per-record redaction calls, `ParseCommitsToTrainingStreamAsync`, `BuildTrainingRecord`, and the private `SubstitutePlaceholders` are removed. | `DECISIONS-...md#D002`, `#T009`, `#T018` |
| `Writers/LlmAssistantWriter.cs` | Implements `IAssistantMessageGenerator`; constructor `(IChatClientFactory, RedactionPolicy)`; `GenerateAsync` forwards `ChatOptions options` to `client.GetResponseAsync`; after the chat call, calls `policy.Redact(llmOutput, RedactionTier.All)`; returns `AssistantMessageGenerated(llmResult, redactedCommit.CommitMessage)` on success, `AssistantMessageAttemptedAndFailed(FallbackContent: null, OriginalAssistantMessage: redactedCommit.CommitMessage)` on failure. | `DECISIONS-...md#D003`, `#D005`, `#D007`, `#D008`, `#T008`, `#T011`, `#T015`, `#T016`, `#T017` |
| `DiffToJsonLib.csproj` | Add `<WarningsAsErrors>CS8509</WarningsAsErrors>`. Add a `<PackageReleaseNotes>` entry calling out the `IGitCommitParser` API break. | `DECISIONS-...md#T004`, `#T018`, `#T020` |

### Tests (`tests/DiffToJsonLib.Tests`)

| File | Role | Cites |
|---|---|---|
| `DiffToJsonLib.Tests.csproj` | TUnit test project, project reference to `DiffToJsonLib` only. Adds `TUnit`, `Verify`. | `DECISIONS-...md#D009`, `#D012`, `#T001`, `#T003`, `#T012` |
| `Fixtures/StubChatClient.cs` | Hand-rolled `IChatClient`; configurable canned response for `GetResponseAsync`; `GetStreamingResponseAsync` throws `NotImplementedException`. | `DECISIONS-...md#T003` |
| `Fixtures/git-log.txt`, `Fixtures/ChatClientSequences/*.txt` | Fixture inputs for parser and generator tests. | `DECISIONS-...md#D002`, `#D012`, `#T006` |
| `Fixtures/{TestName}.verified.txt` | `Verify` golden files. Read-only after the first commit. | `DECISIONS-...md#D012`, `#T012` |
| `Parsing/GitLogParserTests.cs` | Parses fixture git-log text into `RawCommit` sequences; asserts message/diff splitting. | `DECISIONS-...md#D002`, `#T006` |
| `Training/RedactionPolicyTests.cs` | Per-tier field-scope (message-only, diff-only, all, none) and per-string redaction; passthrough on missing entries. | `DECISIONS-...md#D007`, `#T008`, `#T016` |
| `Training/PromptSubstitutorTests.cs` | All five placeholder substitutions; no validation. | `DECISIONS-...md#D004`, `#T010` |
| `Training/DisabledAssistantMessageGeneratorTests.cs` | Returns `AssistantMessageDisabled` with the redacted commit message as `FallbackContent`. | `DECISIONS-...md#D005`, `#T011` |
| `Training/LlmAssistantWriterTests.cs` | Success returns `AssistantMessageGenerated`; failure returns `AssistantMessageAttemptedAndFailed` with redacted `OriginalAssistantMessage`; LLM output is redacted on `All`; `ChatOptions` is forwarded to `IChatClient`. | `DECISIONS-...md#D003`, `#D008`, `#T008`, `#T011`, `#T015`, `#T016`, `#T017` |
| `Training/TrainingExampleBuilderTests.cs` | End-to-end redaction, prompt substitution, and result-type dispatch; `OriginalAssistantMessage` populated in success and failure, null in disabled; `OriginalAssistantMessage` absent only in disabled. | `DECISIONS-...md#D001`, `#D005`, `#D006`, `#D007`, `#T001`, `#T007`, `#T013`, `#T017` |
| `EndToEndRegressionTests.cs` | Runs a fixture git log through the new pipeline with a stub `IChatClient` and asserts the JSONL output matches the `Verify` golden file. Includes a fixture variant that exercises the LLM-failure path. | `DECISIONS-...md#D012`, `#T012`, `#T017` |

### CLI (`DiffToJsonCli`)

| File | Change | Cites |
|---|---|---|
| `Program.cs` | Removes the inline `BuildTrainingRecords` and the local `SubstitutePlaceholders`. Adds CLI composition: build `RedactionPolicy` from a tier→`Redactor` DI dictionary; build `ChatOptions` once via `IChatOptionsBuilder` and assign to `options.Options`; for `format == "training"`, compose `commitParser.ParseCommitsStreamAsync(...)` → `trainingBuilder.BuildAsync(stream, options, ct)` → `trainingWriter.WriteToJsonFileAsync(stream, outputPath, ct)`; for `format == "raw"`, wrap the parser stream in a local async iterator that calls `policy.Redact(record, redactionTier)` per record before yielding. | `DECISIONS-...md#D004`, `#D005`, `#D006`, `#D007`, `#D010`, `#T004`, `#T005`, `#T008`, `#T010`, `#T014`, `#T015`, `#T019` |
| `DiffToJsonCli.csproj` | Add a `<PackageReleaseNotes>` entry calling out the raw-path redaction behavior change (now honors `--redaction`). | `DECISIONS-...md#D010`, `#T019` |

---

## 3. Implementation Order

The T-records are dependency-ordered. The downstream ticket decomposition should respect this order.

1. **Module skeleton** — T001 (test project), T002 (new `GitLogParser`), T004 (sealed record hierarchy with `AttemptedAndFailed` variant — `Generated` and `Disabled` first, `AttemptedAndFailed` added in step 3 below).
2. **Redaction pipeline** — T007 (builder constructor), T008 (policy shape, both `Redact(CommitRecord, ...)` and `Redact(string, ...)`), T010 (substitutor), T005 (options bag with the original four fields).
3. **Result type and generator interface** — T004 (sealed hierarchy, base + `Generated` + `Disabled` first), T011 (interface signature with `CommitRecord`), T017 (add `OriginalAssistantMessage` to `AttemptedAndFailed`), T015 (widen interface with `ChatOptions`; add `Options` to options bag), T016 (writer takes `RedactionPolicy`; redacts LLM output at `All`).
4. **Builder and writer** — T007 (builder constructor signature — already locked), T013 (builder is sealed concrete), T008 (writer drops `RedactionTier`; re-introduces `RedactionPolicy` per T016), T011 + T017 (writer returns the three variants per the amended interface).
5. **Parser refactor** — T009 (parser is pure; builder owns redaction), T018 (delete `ParseCommitsToTrainingStreamAsync`), T002 (thin shell composes `GitLogParser`).
6. **CLI composition** — T014 (DI registration), T005 (CLI pre-resolves prompt templates), T015 (CLI builds `ChatOptions` once), T019 (raw-path local iterator).
7. **`.csproj` and release notes** — T020 (`<WarningsAsErrors>CS8509</WarningsAsErrors>`), T018 release note, T019 release note.
8. **End-to-end regression** — T012 (Verify golden file), D012 (regression test runs the new pipeline against the golden file produced from the inline `BuildTrainingRecords` before deletion).

---

## 4. Acceptance Criteria

The refactor is "done" when:

- The inline `BuildTrainingRecords` in `Program.cs` and the `ParseCommitsToTrainingStreamAsync` method in `GitCommitParser` are deleted; no code in the repository references either.
- `IGitCommitParser` exposes exactly two methods; `GitCommitParser` is a thin shell with one constructor dep (`IProcessInvoker`); `GitLogParser` is git-free and tested with a `TextReader` fixture.
- `TrainingExampleBuilder` is a sealed concrete class; the `BuildAsync` switch expression over `AssistantMessageResult` does not compile if a variant is added without a branch (CS8509 fires).
- `AssistantMessageGenerated` and `AssistantMessageAttemptedAndFailed` both carry `OriginalAssistantMessage`; `AssistantMessageDisabled` does not. Downstream consumers that filter on `OriginalAssistantMessage != null` see the same behavior as the pre-refactor inline code (override attempted → present).
- `IAssistantMessageGenerator.GenerateAsync` takes five parameters including `ChatOptions options`; the `DisabledAssistantMessageGenerator` ignores `ChatOptions`; `LlmAssistantWriter` forwards it to `client.GetResponseAsync`.
- `RedactionPolicy` has two methods: `Redact(CommitRecord, RedactionTier) → CommitRecord` (commit field-scope) and `Redact(string, RedactionTier) → string` (general text, used by the writer for LLM-output redaction at `All`).
- The `Verify` regression test passes: the new pipeline's JSONL output equals the golden file produced from the inline `BuildTrainingRecords` before deletion, for both the success and failure LLM paths.
- The raw format path's JSONL output now respects the `--redaction` flag (previously it was effectively "message only" regardless of the flag); the behavior change is called out in the CLI's `<PackageReleaseNotes>`.
- The library's `<PackageReleaseNotes>` calls out the `IGitCommitParser` API break (from 3 methods to 2).

---

## 5. Forward Risks

- **CS8509 is dead configuration if the builder is later refactored to a method-dispatch pattern.** The `<WarningsAsErrors>` setting is small enough to retire without ceremony, but a contributor who notices the dead config may not realise it was load-bearing for the previous design.
- **The raw-path redaction is wired in a single local async iterator in `Program.cs`.** A future contributor extracting the raw path into a helper must preserve the wrapper or PII is silently unprotected on the raw format. The end-to-end regression test exercises the training path; a parallel test for the raw path's tier-respecting redaction is required.
- **`AssistantMessageDisabled` carries no `OriginalAssistantMessage` field; the asymmetry is intentional because the field would be redundant with `FallbackContent` in the disabled case.** A future contributor may "fix" the asymmetry by adding the field, creating two ways to read the same value.
- **`LlmAssistantWriter` redacts the LLM output only on `RedactionTier.All` (preserving the current asymmetric behavior).** A future contributor adding a new tier (e.g., `RedactionTier.LlmOutput`) must update both the policy's per-tier field-scope and the writer's hard-coded `All` check; the two sites can drift.

---

## Ledger Reference

### D-records cited

- `DECISIONS-DiffToJson-training-example-pipeline.md#D001` — responsibility boundary of the Training Example pipeline module
- `DECISIONS-DiffToJson-training-example-pipeline.md#D002` — where the git-log state machine lives
- `DECISIONS-DiffToJson-training-example-pipeline.md#D003` — what `IAssistantMessageGenerator` returns
- `DECISIONS-DiffToJson-training-example-pipeline.md#D004` — how the prompt template reaches the builder
- `DECISIONS-DiffToJson-training-example-pipeline.md#D005` — how the builder decides whether to call the generator
- `DECISIONS-DiffToJson-training-example-pipeline.md#D006` — builder's role in the pipeline
- `DECISIONS-DiffToJson-training-example-pipeline.md#D007` — where the RedactionTier lives
- `DECISIONS-DiffToJson-training-example-pipeline.md#D008` — fate of the existing LlmAssistantWriter
- `DECISIONS-DiffToJson-training-example-pipeline.md#D009` — test project inclusion and framework
- `DECISIONS-DiffToJson-training-example-pipeline.md#D010` — scope of the refactor (raw format path)
- `DECISIONS-DiffToJson-training-example-pipeline.md#D011` — when OriginalAssistantMessage is set (superseded by T017)
- `DECISIONS-DiffToJson-training-example-pipeline.md#D012` — scope of the TUnit test project

### T-records cited

- `DECISIONS-DiffToJson-training-example-pipeline.md#T001` — test project name and location
- `DECISIONS-DiffToJson-training-example-pipeline.md#T002` — sub-namespace organization for the new library types
- `DECISIONS-DiffToJson-training-example-pipeline.md#T003` — test-time stub strategy for `IChatClient`
- `DECISIONS-DiffToJson-training-example-pipeline.md#T004` — shape of `AssistantMessageResult` (partially superseded by T017)
- `DECISIONS-DiffToJson-training-example-pipeline.md#T005` — `TrainingExampleOptions` field set (partially superseded by T015)
- `DECISIONS-DiffToJson-training-example-pipeline.md#T006` — `GitLogParser` streaming interface shape
- `DECISIONS-DiffToJson-training-example-pipeline.md#T007` — `TrainingExampleBuilder` constructor signature
- `DECISIONS-DiffToJson-training-example-pipeline.md#T008` — `RedactionPolicy` constructor shape
- `DECISIONS-DiffToJson-training-example-pipeline.md#T009` — PII redaction scope and the parser/builder split
- `DECISIONS-DiffToJson-training-example-pipeline.md#T010` — location of the `SubstitutePlaceholders` helper
- `DECISIONS-DiffToJson-training-example-pipeline.md#T011` — `IAssistantMessageGenerator` interface signature (partially superseded by T015 and T017)
- `DECISIONS-DiffToJson-training-example-pipeline.md#T012` — golden-file fixture storage and comparison strategy
- `DECISIONS-DiffToJson-training-example-pipeline.md#T013` — `TrainingExampleBuilder` has no interface
- `DECISIONS-DiffToJson-training-example-pipeline.md#T014` — CLI DI registration shape
- `DECISIONS-DiffToJson-training-example-pipeline.md#T015` — `ChatOptions` plumbing to the LLM call
- `DECISIONS-DiffToJson-training-example-pipeline.md#T016` — LLM-output redaction location
- `DECISIONS-DiffToJson-training-example-pipeline.md#T017` — `OriginalAssistantMessage` in the LLM-failure variant
- `DECISIONS-DiffToJson-training-example-pipeline.md#T018` — fate of `ParseCommitsToTrainingStreamAsync`
- `DECISIONS-DiffToJson-training-example-pipeline.md#T019` — raw-path redaction after the refactor
- `DECISIONS-DiffToJson-training-example-pipeline.md#T020` — exact `.csproj` configuration for the exhaustive-switch warning
