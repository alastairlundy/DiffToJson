# Decision Ledger — Training Example Pipeline

> Session opened 2026-06-23 to deepen the Training Example pipeline module
> in DiffToJson. Concerns co-located today in the inline `BuildTrainingRecords`
> at `src/DiffToJsonCli/Program.cs:361-430` and partially duplicated at
> `src/DiffToJsonLib/GitCommitParser.cs:131-232`.
> 
> Re-established 2026-07-04 after the working tree was reset and the file was lost.

### [D001] — responsibility boundary of the Training Example pipeline module

- **Resolved Answer**: "(2) looks like the right choice." — Option 2, Assembler plus named adapters.
- **Normalized Requirement**: A `TrainingExampleBuilder` module in `DiffToJsonLib` shall own prompt substitution, ChatML message list assembly, Original Assistant Message tracking, and Provenance/Legal wrapping. The builder shall take a `RedactionPolicy` and an `IAssistantMessageGenerator` as constructor dependencies. The builder shall not own redaction, the LLM call, or the git log state machine.
- **Constraints**: The two named adapters (`RedactionPolicy`, `IAssistantMessageGenerator`) shall be distinct library types, not internal classes of the builder. The CLI's DI registration shall wire the concrete `TrainingExampleBuilder`, the concrete `RedactionPolicy`, and the default `IAssistantMessageGenerator` adapter together.

### [D002] — where the git-log state machine lives

- **Resolved Answer**: "(1) - Extract to a separate module" — Option 1, separate `GitLogParser` module.
- **Normalized Requirement**: A new `DiffToJsonLib.Parsing.GitLogParser` (or equivalent namespace) shall own the state machine that splits `git --no-pager log -p` output into `(commit message, diff)` pairs. The module shall expose a `Parse(TextReader)` (or `IAsyncEnumerable`) interface that does not require spawning `git` to test. `GitCommitParser` shall shrink to a thin shell that spawns the process and wraps each pair in a `CommitRecord`.
- **Constraints**: The `(string Message, string Diff)` tuple shape is part of the public contract — any future caller must produce records of the same shape so the deduplication cannot drift back.

### [D003] — what `IAssistantMessageGenerator` returns

- **Resolved Answer**: "(3)" — Option 3, result type.
- **Normalized Requirement**: `IAssistantMessageGenerator` shall return a result type that distinguishes at least three states: the LLM produced a usable assistant message; the LLM was disabled or absent (fall back to commit message); the LLM was attempted and failed (fall back to commit message and preserve the original for evaluation). The builder's branch table over the result shall be exhaustive.
- **Constraints**: The result type shall expose enough information for the builder to set `OriginalAssistantMessage` correctly in every variant. The default adapter for the existing `LlmAssistantWriter` shall be expressible as a one-constructor dependency with no other library types added solely to bridge the signature.

### [D004] — how the prompt template reaches the builder

- **Resolved Answer**: "(1)" — Option 1, CLI pre-resolves; builder consumes resolved templates.
- **Normalized Requirement**: The CLI shall resolve `PromptPresets.Get(promptStyle)` together with `systemPromptOverride` and `userPromptOverride` into a single `PromptTemplate` value, and resolve `llmOverridePrompt` (if set) into a separate `string?`, before constructing `TrainingExampleOptions`. The builder shall not depend on `PromptPresets`. The builder shall not perform placeholder validation.
- **Constraints**: Placeholder validation lives in the CLI action handler. A future caller that wants the builder to do the resolution has to re-implement the resolution step; the library shall not provide a convenience that bypasses the CLI's pre-resolution.

### [D005] — how the builder decides whether to call the generator

- **Resolved Answer**: "(1) makes sense" — Option 1, generator unconditional in the constructor; options flag drives the call.
- **Normalized Requirement**: `TrainingExampleBuilder` shall take a non-nullable `IAssistantMessageGenerator` as a constructor dependency. `TrainingExampleOptions` shall carry a `bool LlmAssistantOutput` field. When the flag is false, the builder shall skip the generator call and treat the assistant content as the (redacted) commit message. The library shall provide a `DisabledAssistantMessageGenerator` adapter that returns the fallback result for the disabled case.
- **Constraints**: The constructor signature is fixed — no nullable generator dep, no per-build generator parameter. The `DisabledAssistantMessageGenerator` is a public library type so tests can pin down its behaviour.

### [D006] — builder's role in the pipeline

- **Resolved Answer**: "(1) Is the obvious call for now. If an orchestrator is needed at a later date it can be introduced." — Option 1, transformer; orchestrator may be added later if needed.
- **Normalized Requirement**: `TrainingExampleBuilder` shall expose `BuildAsync(IAsyncEnumerable<CommitRecord> source, TrainingExampleOptions options, CancellationToken ct) : IAsyncEnumerable<CommitTrainingRecord>`. The builder shall not depend on `IGitCommitParser`, on the file system, or on `git`. The CLI shall compose `commitParser.ParseCommitsStreamAsync(...)` → `trainingBuilder.BuildAsync(stream, options, ct)` → `trainingWriter.WriteToJsonFileAsync(stream, outputPath, ct)` explicitly.
- **Constraints**: An orchestrator convenience method (e.g. `BuildFromRepositoryAsync(DirectoryInfo, ...)`) is explicitly out of scope for this work and may be added in a follow-up if a non-CLI caller needs it. The builder's constructor dependency list shall not grow to include `IGitCommitParser` or any file-system type as a side effect of this constraint.

### [D007] — where the RedactionTier lives

- **Resolved Answer**: "(2) seems like the right decision for now" — Option 2, tier in the options; the policy is a singleton.
- **Normalized Requirement**: `RedactionPolicy` shall be a stateless wrapper around a `Redactor`, registered once in DI. The policy's `Redact(CommitRecord, RedactionTier) → CommitRecord` shall take the tier per call. `TrainingExampleOptions` shall carry a `RedactionTier` field. The builder shall pass `options.RedactionTier` to the policy.
- **Constraints**: The policy's API shall not grow to require a tier in its constructor; the DI graph shall not re-register the policy per build. If a future method on the policy needs the tier (e.g. a `Redact(CommitTrainingRecord, RedactionTier)` overload), that overload shall also take the tier as a parameter, not fold it back into the constructor.

### [D008] — fate of the existing LlmAssistantWriter

- **Resolved Answer**: "(1)" — Option 1, refactor `LlmAssistantWriter` in place.
- **Normalized Requirement**: `LlmAssistantWriter` shall remain a single library type and shall implement `IAssistantMessageGenerator` after refactoring. Its constructor shall drop the `RedactionTier` parameter; its method shall return the new `AssistantMessageResult` (no nullable `string`); its existing chat call, retry logic, and response extraction shall remain. The upcoming `preserve-reasoning-in-assistant` blueprint's `ChatOptions` parameter and reasoning-content composition shall land in this same class.
- **Constraints**: The library shall not introduce a parallel `AssistantMessageGenerator` type that wraps or shadows `LlmAssistantWriter`. The `LlmAssistantWriter` class shall be the single boundary for response-side composition, matching the upcoming blueprint's D-records.

### [D009] — test project inclusion and framework

- **Resolved Answer**: "(1) - Testing should use TUnit"
- **Normalized Requirement**: A new test project shall be added to `DiffToJsonApp.slnx` as part of this refactor. The test framework shall be TUnit. The test project shall cover the new modules introduced by this refactor (`GitLogParser`, `TrainingExampleBuilder`, `RedactionPolicy`, `IAssistantMessageGenerator` adapter, `DisabledAssistantMessageGenerator`) and the refactored `LlmAssistantWriter`. The AGENTS.md `Build` command shall continue to work; a `Test` command shall be added if one does not already exist.
- **Constraints**: The test framework choice is TUnit. The test project shall not depend on `git`, on the file system, or on any network resource; all tests shall run in-process with fixture inputs. The new test project shall follow the same directory layout convention as the existing library and CLI projects.

### [D010] — scope of the refactor (raw format path)

- **Resolved Answer**: "(1)" — Option 1, leave the raw path alone.
- **Normalized Requirement**: This refactor shall touch only the training format path. The CLI's `format == "raw"` branch shall continue to call `commitParser.ParseCommitsStreamAsync(...)` and `diffJsonFileWriter.WriteToJsonFileAsync(...)` directly. The new `GitLogParser` benefits both paths because the parser is shared, but no new builder module shall be introduced for the raw path.
- **Constraints**: The CLI's action handler shall retain a `format` branch. No `CommitRecordBuilder` (or analogous shallow module) shall be introduced. If a future refactor wants to apply the same depth improvements to the raw path, it shall be tracked as a separate decision ledger, not folded into this one.

### [D011] — when OriginalAssistantMessage is set

- **Resolved Answer**: "(2)" — Option 2, set only when the LLM produced a message.
- **Normalized Requirement**: `OriginalAssistantMessage` shall be present only in the "generated" variant of the result type. The field shall be absent in both the "disabled" and the "attempted-and-failed" variants. The model shall not grow a nullable `OriginalAssistantMessage` to encode the "attempted-and-failed" case; the absence of the field is the signal.
- **Constraints**: The behavioural change from the existing inline code is accepted. Downstream consumers that filtered on `OriginalAssistantMessage != null` to count override attempts will see fewer records; this is a deliberate semantic change, not a bug. `CONTEXT.md` is consistent with this rule and does not need to be updated.

### [D012] — scope of the TUnit test project

- **Resolved Answer**: "(1)" — Option 1, all new + refactored modules, with regression tests against a golden file.
- **Normalized Requirement**: The TUnit test project introduced in D009 shall cover the new modules (`GitLogParser`, `TrainingExampleBuilder`, `RedactionPolicy`, `IAssistantMessageGenerator` adapter, `DisabledAssistantMessageGenerator`) and the refactored `LlmAssistantWriter`. The test project shall include a regression test that runs a fixture git log through a stub `IChatClient` and asserts the new pipeline's JSONL output equals a golden file produced from the existing inline `BuildTrainingRecords` before deletion.
- **Constraints**: The golden file shall be generated once from the existing inline code, committed to the test project's fixture directory, and treated as read-only thereafter. A mismatch between the new pipeline's output and the golden file shall fail the regression test. No test shall depend on `git`, on the file system outside the test project's fixture directory, or on any network resource.

### [T001] — test project name and location

- **Resolved Answer**: "(1) makes sense" — Option 1, `tests/DiffToJsonLib.Tests/DiffToJsonLib.Tests.csproj`.
- **Normalized Requirement**: A new TUnit test project shall be added at `tests/DiffToJsonLib.Tests/DiffToJsonLib.Tests.csproj` and referenced from `DiffToJsonApp.slnx`. The test project shall declare a project reference to `DiffToJsonLib` only; it shall not declare a project reference to `DiffToJsonCli`. The test project shall follow the directory layout of the existing library and CLI projects under the top-level `tests/` mirror.
- **Constraints**: The path and project name are fixed. CLI composition testing is out of scope for this ledger. If a future ledger requires CLI-seam tests, a second test project shall be added at that time, not pre-emptively. The test project name shall not be renamed after the first commit without also renaming the directory.
- **Cites**: D009, D012

### [T002] — sub-namespace organization for the new library types

- **Resolved Answer**: "(1) but we should have an Abstractions folder and namespace under Training for interfaces and abstract classes" — Option 1, single feature namespace `DiffToJsonLib.Training`, with `DiffToJsonLib.Training.Abstractions` for interfaces and abstract classes; `GitLogParser` stays in `DiffToJsonLib.Parsing` (per D002).
- **Normalized Requirement**: Library types introduced by this refactor shall be placed as follows. `DiffToJsonLib.Parsing.GitLogParser` (per D002). `DiffToJsonLib.Training` for the concrete types: `TrainingExampleBuilder`, `RedactionPolicy`, `DisabledAssistantMessageGenerator`, `AssistantMessageResult`, and `TrainingExampleOptions`. `DiffToJsonLib.Training.Abstractions` for the interface `IAssistantMessageGenerator` and any other interface or abstract class added in this refactor.
- **Constraints**: Abstract types and interfaces go in `DiffToJsonLib.Training.Abstractions`; concrete types stay in the parent `DiffToJsonLib.Training` namespace. The CLI composition shall import both namespaces. The `GitLogParser` namespace is fixed by D002 and shall not be moved. A future addition to this refactor that introduces a new interface shall follow the same split, not be promoted to the parent namespace. The `Abstractions` sub-namespace shall not contain concrete types.
- **Cites**: D001, D002

### [T003] — test-time stub strategy for `IChatClient`

- **Resolved Answer**: "(1)" — Option 1, hand-rolled `StubChatClient` in test fixtures, no mocking library.
- **Normalized Requirement**: The test project shall provide a hand-rolled `StubChatClient` (in `tests/DiffToJsonLib.Tests/Fixtures/StubChatClient.cs` or equivalent) that implements `IChatClient` and returns a configurable canned response (or sequence of responses) for `GetResponseAsync`. `GetStreamingResponseAsync` shall throw `NotImplementedException`. The test project shall not add NSubstitute, Moq, or any other dynamic-proxy mocking library as a package dependency.
- **Constraints**: The stub lives in the test project only and is not part of the library's public surface. The stub's `GetResponseAsync` shall accept a configurable response value via constructor or property. The stub shall not grow a call-argument recorder unless a future test demonstrates the need. The test project's `csproj` shall not declare `NSubstitute`, `Moq`, `FakeItEasy`, or `Castle.DynamicProxy`.
- **Cites**: D009, D012

### [T004] — shape of `AssistantMessageResult`

- **Resolved Answer**: "(1)" — Option 1, sealed record hierarchy.
- **Normalized Requirement**: `AssistantMessageResult` shall be an abstract `record` declared in `DiffToJsonLib.Training` with three `public sealed record` variants: `AssistantMessageGenerated(string Content, string OriginalAssistantMessage)`, `AssistantMessageDisabled(string FallbackContent)`, and `AssistantMessageAttemptedAndFailed(string? FallbackContent)`. The base type shall declare a private constructor to seal the hierarchy against external derivation. The `IAssistantMessageGenerator` method shall return `AssistantMessageResult`. The builder's branch over the result shall be a `switch` expression on the type.
- **Constraints**: The base type shall not declare an `OriginalAssistantMessage` field — D011's "absence is the signal" is structural (the variant simply has no such property), not a runtime convention. The `Disabled` and `AttemptedAndFailed` variants shall not carry an `OriginalAssistantMessage` property. The library's `.csproj` shall enable the C# 12+ "missing switch cases" warning so the builder's exhaustive match is enforced at compile time. No third-party package (`OneOf`, etc.) shall be added for this type.
- **Cites**: D003, D011

### [T005] — `TrainingExampleOptions` field set

- **Resolved Answer**: "(1) Follow the D-records as prescribed" — Option 1, exactly the four fields the D-records require.
- **Normalized Requirement**: `TrainingExampleOptions` shall be a `public sealed record` declared in `DiffToJsonLib.Training` with primary-constructor properties: `PromptTemplate Template`, `string? LlmOverridePrompt`, `bool LlmAssistantOutput`, `RedactionTier Tier`. The builder's `BuildAsync` method shall take `(IAsyncEnumerable<CommitRecord> source, TrainingExampleOptions options, CancellationToken cancellationToken)`. The bag shall not declare `RepoName`, `RepoUrl`, `License`, or any extension/dictionary property.
- **Constraints**: The bag's surface is a literal restatement of D004 (Template, LlmOverridePrompt), D005 (LlmAssistantOutput), and D007 (Tier). `CancellationToken` is a method parameter, not a bag field. Provenance/legal metadata on `CommitTrainingRecord` is sourced from `CommitRecord` per D006, not from the options bag. The bag shall not grow a `Dictionary<string, string>` extension property without a new D-record authorising the field.
- **Cites**: D004, D005, D006, D007

### [T006] — `GitLogParser` streaming interface shape

- **Resolved Answer**: "(1) [accept Option 1 as the implied-by-D006 choice]" — Option 1, `IAsyncEnumerable<RawCommit> ParseAsync(TextReader reader, CancellationToken ct)`, where `RawCommit` is a `record (string Message, string Diff)`.
- **Normalized Requirement**: `GitLogParser` shall expose `IAsyncEnumerable<RawCommit> ParseAsync(TextReader reader, CancellationToken ct)`, where `RawCommit` is a `public sealed record (string Message, string Diff)` declared in `DiffToJsonLib.Parsing`. The parser shall be `git`-free: it shall not spawn a process, read from the file system, or hold a reference to any git-related type. The `TextReader` shall be supplied by the caller. The tuple shape from D002 shall be the public contract of `RawCommit`.
- **Constraints**: The interface shape is fixed to async-stream. No sync `Parse(TextReader)` overload shall be exposed — D002's "or" clause is resolved in favour of `IAsyncEnumerable`. The parser shall not read from a `Stream` directly; a `Stream`-to-`TextReader` adapter (e.g., `StreamReader` with `leaveOpen: true`) is the caller's responsibility. `RawCommit` shall be a positional record whose property names are `Message` and `Diff`; renaming either is a breaking change.
- **Cites**: D002, D006

### [T007] — `TrainingExampleBuilder` constructor signature

- **Resolved Answer**: "(1) - Follow the D-records" — Option 1, exactly the two deps the D-records require.
- **Normalized Requirement**: `TrainingExampleBuilder` shall have a public constructor with signature `(RedactionPolicy redactor, IAssistantMessageGenerator assistant)`. The builder shall not declare a logger, telemetry, clock, or other infrastructure dependency. The builder's `BuildAsync` method shall take `(IAsyncEnumerable<CommitRecord> source, TrainingExampleOptions options, CancellationToken cancellationToken) → IAsyncEnumerable<CommitTrainingRecord>` per D006.
- **Constraints**: The constructor parameter list is fixed to two. The library's `.csproj` shall not declare `Microsoft.Extensions.Logging.Abstractions`, `OpenTelemetry.Api`, or any equivalent observability package as a result of this refactor. Observability for per-commit fallback, generator variant, and redaction tier shall be instrumented at the seam (LlmAssistantWriter, RedactionPolicy), not at the builder. A future addition of a third constructor parameter requires a new D-record or T-record authorising the field.
- **Cites**: D001, D005, D006

### [T008] — `RedactionPolicy` constructor shape (uses Microsoft.Extensions.Compliance built-in `Redactor` abstract class)

- **Resolved Answer**: Revised after correction: `IRedactorProvider` does not exist in `Microsoft.Extensions.Compliance.Redaction`. The built-in type is the abstract class `Redactor`. The policy shall take a tier→`Redactor` map provided by DI.
- **Normalized Requirement**: `RedactionPolicy` shall take `IReadOnlyDictionary<RedactionTier, Redactor>` (where `Redactor` is `Microsoft.Extensions.Compliance.Redaction.Redactor`) as its single constructor dependency. The policy's `Redact(CommitRecord, RedactionTier) → CommitRecord` method shall look up the `Redactor` for the given tier in the dictionary; if the tier is `RedactionTier.None` or the dictionary has no entry for the tier, the policy shall return the input `CommitRecord` unchanged (passthrough). When a `Redactor` is found, the policy shall call `redactor.Redact(commit.CommitMessage)` and/or `redactor.Redact(commit.Diff)` according to the tier's field-scope (matching the existing behaviour at `Program.cs:374-380`).
- **Constraints**: The policy depends on the built-in `Redactor` abstract class from `Microsoft.Extensions.Compliance.Redaction`, not on a custom interface or a provider. The tier→`Redactor` map is constructed in the CLI's DI registration (e.g., `new Dictionary<RedactionTier, Redactor> { [RedactionTier.Message] = regexPii, [RedactionTier.Diff] = regexPii, [RedactionTier.All] = regexPii }`); a `RedactionTier.None` entry is optional (passthrough is the policy's default for missing entries). The policy's `Redact` method returns a new `CommitRecord` via `with` expression; the input is not mutated. The CLI's existing `services.AddRedaction(r => r.SetFallbackRedactor<RegexPiiRedactor>())` registration (at `Program.cs:63-66`) is preserved for the `GitCommitParser` path that still uses `IRedactorProvider` during the transition; the new policy path does not depend on it. A future tier is added by extending the `RedactionTier` enum and adding an entry to the DI dictionary; the policy's switch over tier is extended accordingly.
- **Cites**: D007, D008

### [T009] — PII redaction scope and the parser/builder split

- **Resolved Answer**: "(1) We should localize the redactions to the builder." — Option 1, parser is pure; builder owns all redaction; LLM sees redacted content.
- **Normalized Requirement**: `GitLogParser` and the thin `GitCommitParser` shell shall produce `CommitRecord` with the raw `CommitMessage` and raw `Diff` (no redaction at the parser). The builder shall call `RedactionPolicy.Redact(commit, options.Tier)` first, then use the redacted strings for prompt substitution (system, user, and the LLM override prompt) and pass the redacted strings to `IAssistantMessageGenerator`. The LLM user prompt shall be built from the redacted message and diff. The `CommitTrainingRecord` emitted by the builder shall carry the redacted values.
- **Constraints**: The `GitCommitParser` class's existing `IRedactorProvider` constructor dependency and the per-record redaction at `GitCommitParser.cs:123` shall be removed. The `GitLogParser` state machine (D002, T006) shall not reference any redaction type. The `RedactionPolicy` is the single source of truth for redaction; D007's "the policy's API shall not grow to require a tier in its constructor" is preserved. The raw path (D010) is responsible for its own redaction; the CLI either calls the policy explicitly at the raw branch in `Program.cs` or the raw path's redaction is tracked in a follow-up ledger.
- **Cites**: D002, D006, D007, D010

### [T010] — location of the `SubstitutePlaceholders` helper

- **Resolved Answer**: "(2)" — Option 2, static method on a new `PromptSubstitutor` class in `DiffToJsonLib.Training`.
- **Normalized Requirement**: A new `public static class PromptSubstitutor` shall be declared in `DiffToJsonLib.Training` with a single `public static string Substitute(string template, string diff, string commitMessage, string repoName, string license, string repoUrl)` method. The substitutor shall perform the five existing placeholder substitutions (`{diff}`, `{commitMessage}`, `{repoName}`, `{license}`, `{repoUrl}`) via `string.Replace`. The builder's per-commit loop shall call `PromptSubstitutor.Substitute(...)` for the system template, the user template, and the LLM override prompt.
- **Constraints**: The substitutor is a pure function with no I/O, no logging, no state. The substitutor shall not perform placeholder validation — D004's CLI-side validation is preserved. A new placeholder added to the prompt templates is added as a new parameter to `Substitute(...)`, not as a side-channel. The substitutor shall not be exposed as an extension method on `string`. The existing static `SubstitutePlaceholders` in `Program.cs:50-58` and `GitCommitParser.cs:234-240` shall be removed once the builder calls the new helper; the raw path (D010) is unaffected because it does not substitute placeholders.
- **Cites**: D004, D006

### [T011] — `IAssistantMessageGenerator` interface signature and how `DisabledAssistantMessageGenerator` constructs its result

- **Resolved Answer**: "Okay. Let's use that" — revised Option 1, interface takes `CommitRecord` (with redacted fields) plus the two derived prompts.
- **Normalized Requirement**: `IAssistantMessageGenerator` (in `DiffToJsonLib.Training.Abstractions`) shall expose `Task<AssistantMessageResult> GenerateAsync(string systemPrompt, string userPrompt, CommitRecord redactedCommit, CancellationToken cancellationToken)`. The builder shall construct the `CommitRecord` passed to the generator with redacted `Diff` and redacted `CommitMessage` fields (using `RedactionPolicy.Redact(commit, options.Tier)` as the source). `DisabledAssistantMessageGenerator.GenerateAsync(...)` shall return `new AssistantMessageDisabled(FallbackContent: redactedCommit.CommitMessage)`. The refactored `LlmAssistantWriter` shall return `new AssistantMessageGenerated(llmResult, redactedCommit.CommitMessage)` on success and `new AssistantMessageAttemptedAndFailed(FallbackContent: null)` on failure. The generator shall not receive the raw `CommitRecord`; the builder is the only point that calls `RedactionPolicy.Redact(...)`.
- **Constraints**: The interface takes `CommitRecord`, not individual string parameters for the commit data. The interface takes the two derived prompts (`systemPrompt`, `userPrompt`) as separate parameters; they are not folded into a `GenerationContext` type. The generator ignores `redactedCommit.Diff`, `RepoName`, `License`, and `RepoUrl` (matching the current `LlmAssistantWriter` implementation at `LlmAssistantWriter.cs:33-36`, which only reads the two prompts). A future adapter that needs more commit fields shall extend the interface via a new D-record or T-record, not by introducing a context type without one. The `DisabledAssistantMessageGenerator` populates `FallbackContent` with the redacted commit message; the `FallbackContent` field is not dead.
- **Cites**: D003, D005, D008, D011

### [T012] — golden-file fixture storage and comparison strategy

- **Resolved Answer**: "(3)" — Option 3, use the `Verify` snapshot-testing library.
- **Normalized Requirement**: The test project shall add the `Verify` package as a dependency. The regression test introduced in D012 shall call `Verifier.Verify(actualJsonl)` with the new pipeline's JSONL output for the fixture git log. Verify shall manage the `.verified.txt` storage, diffing, and scrubbing. The golden file shall be stored under the test project's fixture directory using Verify's default naming convention (e.g., `Fixtures/{TestName}.verified.txt`).
- **Constraints**: The test project's `.csproj` shall declare the `Verify` package. The regression test shall not use byte-for-byte string comparison or line-by-line parsed JSON comparison; Verify is the canonical comparison mechanism. The `.verified.txt` file is committed to the test project's fixture directory and is treated as read-only except via Verify's intentional update flow. A future contributor shall not verify the `.received.txt` file as a shortcut — doing so is a code-review smell. The test project shall not add the `ApprovalTests` package; `Verify` is the chosen library.
- **Cites**: D012

### [T013] — `TrainingExampleBuilder` has no interface; it is a sealed concrete class

- **Resolved Answer**: "(a)" — Record a T-record stating `TrainingExampleBuilder` is a sealed concrete class with no interface; revisit if a second implementation emerges.
- **Normalized Requirement**: `TrainingExampleBuilder` shall be declared as a `public sealed class` in `DiffToJsonLib.Training`. The class shall not have a corresponding `ITrainingExampleBuilder` interface in `DiffToJsonLib.Training.Abstractions` or any other namespace. The CLI composition layer (`Program.cs`) shall reference the concrete `TrainingExampleBuilder` type, not an interface.
- **Constraints**: The builder's sealed-concrete status is a deliberate departure from the project's adapter-interface convention (`IGitCommitParser`, `IChatClientFactory`, `IDiffJsonFileWriter`, `IDiffTrainingJsonFileWriter`, `ILicenseAnalyzer`). The convention applies to swappable components with multiple implementations or mock-worthy seams; the builder has neither today. A second implementation (e.g., a test-only fake builder, a future orchestrator per `D006`) is the trigger for revisiting this decision. If a future T-record introduces an `ITrainingExampleBuilder`, the CLI composition layer shall be updated to depend on the interface, and the existing concrete `TrainingExampleBuilder` shall implement it without behavioural change. The builder's test project (`tests/DiffToJsonLib.Tests`) may test the builder directly against its concrete type; a test fake shall not be introduced to compensate for the missing interface.
- **Cites**: D001, D006, D009

### [T014] — CLI DI registration shape

- **Resolved Answer**: "(1) There's no real penalty to registering the LlmAssistantWriter if it doesn't get used." — Option 1, `LlmAssistantWriter` as `IAssistantMessageGenerator`; builder checks the flag.
- **Normalized Requirement**: The CLI's DI registration shall add `services.AddSingleton<IAssistantMessageGenerator, LlmAssistantWriter>(); services.AddSingleton<RedactionPolicy>(); services.AddSingleton<TrainingExampleBuilder>();` after the existing `services.AddRedaction(r => r.SetFallbackRedactor<RegexPiiRedactor>())` line. The existing `services.AddSingleton<IGitCommitParser, GitCommitParser>()` and writer registrations stay. The builder's `BuildAsync` shall check `options.LlmAssistantOutput` and either call the generator or skip it (treating the redacted commit message as the assistant content per D005).
- **Constraints**: The CLI's DI graph has no conditional registration based on the `LlmAssistantOutput` flag. The disabled adapter is not registered in the CLI's DI graph; it is registered by the test project's fixture (D005, T011). The `LlmAssistantWriter` is constructed unconditionally at DI resolution time; the penalty of constructing an unused LLM client factory is accepted because the writer is cheap to construct and the LLM call is the expensive step (which the builder skips when the flag is false). A future per-invocation flag flows through `TrainingExampleOptions`, not through DI.
- **Cites**: D001, D005, D007, D008

### [T015] — `ChatOptions` plumbing to the LLM call after the refactor

- **Resolved Answer**: "Option 1"
- **Normalized Requirement**: `IAssistantMessageGenerator.GenerateAsync` shall be widened to `Task<AssistantMessageResult> GenerateAsync(string systemPrompt, string userPrompt, CommitRecord redactedCommit, ChatOptions options, CancellationToken cancellationToken)`. `TrainingExampleOptions` shall grow a fifth field `ChatOptions Options`. The CLI shall build the `ChatOptions` once via `IChatOptionsBuilder` and assign it to `options.Options` before calling `BuildAsync`. The builder shall pass `options.Options` to every generator call inside its loop. `LlmAssistantWriter.GenerateAsync` shall forward `options` to `client.GetResponseAsync`. `DisabledAssistantMessageGenerator.GenerateAsync` shall accept and ignore the parameter.
- **Constraints**: `Supersedes: T005 (partially)`, `Supersedes: T011 (partially)`. The options bag's surface now includes `ChatOptions Options` as the fifth field, alongside the four fields fixed by T005. The interface method signature is widened from four to five parameters; the new parameter is required and shall be a non-nullable `ChatOptions`. T007's builder constructor (two deps) is unchanged. D006's `BuildAsync` method signature (three params) is unchanged. T014's DI registration of `LlmAssistantWriter` as `IAssistantMessageGenerator` is unchanged. The `StubChatClient` (T003) is unaffected — `ChatOptions` is consumed by the writer, not by the chat client stub. A regression test shall assert that a non-default `ChatOptions` (e.g., one with a `ReasoningEffort`) round-trips through the pipeline to the chat client invocation; the regression test introduced in D012/T012 shall include a fixture variant that exercises this path.
- **Cites**: D001, D005, D006, D008

### [T016] — LLM-output redaction location after the refactor

- **Resolved Answer**: "Option 2 it is"
- **Normalized Requirement**: `LlmAssistantWriter` shall take `RedactionPolicy` as its second constructor dependency (alongside `IChatClientFactory`). After the chat call and before composing the `AssistantMessageResult`, the writer shall call `policy.Redact(llmOutput, RedactionTier.All)` to redact the LLM response, preserving the current asymmetric behavior (LLM output redacted only on `All`, not on `Message` or `Diff`). `RedactionPolicy` shall grow a `public string Redact(string text, RedactionTier tier)` method that returns the input unchanged when `tier == RedactionTier.None` or the dictionary has no entry for the tier, and otherwise calls `redactor.Redact(text)`. The CLI's DI registration shall resolve `RedactionPolicy` from the service provider and pass it to the writer's factory.
- **Constraints**: The writer's constructor signature is `(IChatClientFactory chatClientFactory, RedactionPolicy policy)` — two deps, matching the pre-T008 dep count (the `RedactionTier` parameter T008 dropped is replaced by `RedactionPolicy`). T007's two-dep constraint applies to the BUILDER (`TrainingExampleBuilder`) and is unaffected. T014's DI registration line `services.AddSingleton(sp => new LlmAssistantWriter(sp.GetRequiredService<IChatClientFactory>(), redactionTier))` is amended to `services.AddSingleton(sp => new LlmAssistantWriter(sp.GetRequiredService<IChatClientFactory>(), sp.GetRequiredService<RedactionPolicy>()))`; `RedactionPolicy` is registered before the writer in the DI graph per T014. T008's policy API grows one new method (`Redact(string, RedactionTier) → string`); the existing `Redact(CommitRecord, RedactionTier) → CommitRecord` method is unchanged. The `tier == All` behavior for LLM-output redaction is hard-coded at the writer; the policy's per-tier field-scope applies to commit fields only. `DisabledAssistantMessageGenerator` does not redact and ignores redaction concerns. A regression test shall assert that the writer redacts the LLM output when the policy's `All` entry has a redactor and does not redact when the policy has no `All` entry; the `StubChatClient` (T003) is unaffected because redaction happens in the writer, not in the chat client.
- **Cites**: D007, D008

### [T017] — `OriginalAssistantMessage` in the LLM-failure variant

- **Resolved Answer**: "Let's go with Option 2"
- **Normalized Requirement**: `AssistantMessageAttemptedAndFailed` shall grow an `OriginalAssistantMessage` field carrying the redacted commit message. The variant's new shape is `AssistantMessageAttemptedAndFailed(string? FallbackContent, string OriginalAssistantMessage)`. `AssistantMessageDisabled` shall remain unchanged (`string FallbackContent` only) because the field would be redundant with `FallbackContent` in the disabled case. The refactored `LlmAssistantWriter` shall return `new AssistantMessageAttemptedAndFailed(FallbackContent: null, OriginalAssistantMessage: redactedCommit.CommitMessage)` on failure. The builder's switch over the result type shall set `CommitTrainingRecord.OriginalAssistantMessage` from the variant's field in the `Generated` and `AttemptedAndFailed` cases, and to `null` (absent) in the `Disabled` case. CONTEXT.md stands as written and does not need to be updated.
- **Constraints**: `Supersedes: D011`, `Supersedes: T004 (partially)`, `Supersedes: T011 (partially)`. The D011 "absence is the signal" rule is reversed: `OriginalAssistantMessage` is now present in both the `Generated` and `AttemptedAndFailed` variants, and absent only in the `Disabled` variant. T004's sealed-record hierarchy shape is amended: the `AssistantMessageAttemptedAndFailed` variant grows the field; the base type still declares no `OriginalAssistantMessage` field; the C# "missing switch cases" warning (T004) still applies because the three-variant hierarchy is preserved. T011's failure-path return is amended from `new AssistantMessageAttemptedAndFailed(FallbackContent: null)` to `new AssistantMessageAttemptedAndFailed(FallbackContent: null, OriginalAssistantMessage: redactedCommit.CommitMessage)`. The disabled-case asymmetry is intentional: `AssistantMessageDisabled(FallbackContent)` already carries the redacted commit message, so adding a second field for the same value would be redundant. The downstream semantics revert to the current inline code's behavior: `OriginalAssistantMessage` is present whenever LLM override is attempted (success or failure), absent when LLM override is disabled. A regression test shall assert that `OriginalAssistantMessage` is populated in both the success and failure LLM paths and is `null` only in the disabled path; the D012/T012 golden-file regression test shall include a fixture variant that exercises the LLM-failure path.
- **Cites**: D003, D011
