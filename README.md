# DiffToJson

[![Latest NuGet Version](https://img.shields.io/nuget/v/DiffToJson?style=flat-square)](https://nuget.org/packages/DiffToJson)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DiffToJson?style=flat-square)](https://nuget.org/packages/DiffToJson)
![GitHub License](https://img.shields.io/github/license/alastairlundy/DiffToJson?style=flat-square)
![OpenSSF Scorecard Score](https://img.shields.io/ossf-scorecard/github.com/alastairlundy/DiffToJson?style=flat-square&label=OpenSSF%20Scorecard%20Score)


A CLI for detecting and serializing Git commit Diffs and commit messages from a local Git repository to a .JSONL file.

This can be useful for preparing git commit diffs and message data for training AI/ML models or similar use cases.

**NOTE**: Whilst the CLI implements a Regex pattern matching based PII detector for detecting email addresses in commit messages and diffs (per the `--redaction` tier) and redacting them, redaction of email addresses is not guaranteed. If commit messages or diffs contain sensitive information, conduct a human review of the output .JSONL file.

## Output Formats

Two output formats are available, selected via `--format`:

- **`raw`** (legacy): PascalCase JSONL with flat fields — `Diff`, `CommitMessage`, `RepoName`, `License`, `RepoUrl`. Note: `--format raw` now honors the `--redaction` flag. Previously, the raw path always redacted commit messages.
- **`training`** (default): camelCase JSONL shaped for LLM post-training pipelines. Each record is a Training Example with a ChatML `messages` array, `provenance`, `legal`, and optionally `originalAssistantMessage`. See [Training Example Output](#training-example-output) below.

### Documented Information (raw format)
* The Git Diff
* The Git Commit Message associated with the diff
* The license Name if a LICENSE.md, LICENSE.txt, or LICENSE file is present in the repo directory — An LLM call is required to compute this. As a fallback "Unknown" is returned otherwise.
* The Git project name — Obtained from the Git Repo Directory name
* The Git Repo URL if provided by the CLI caller.

### Training Example Output

When `--format training` (default), each line of the JSONL file is a single Training Example in camelCase:

```json
{
  "messages": [
    {"role": "system", "content": "You are a software engineer. You write high-quality commit messages that follow best practices."},
    {"role": "user", "content": "Write a commit message for the diff in the repository 'my-repo':\n<diff text>"},
    {"role": "assistant", "content": "<commit message or LLM-generated response>"}
  ],
  "provenance": {"repoName": "my-repo", "repoUrl": "https://github.com/example/my-repo"},
  "legal": {"license": "MIT"},
  "originalAssistantMessage": "<human-written message, present only with --llm-assistant-output>"
}
```

The exact system/user text depends on the selected `--prompt-style` preset and any `--system-prompt` / `--user-prompt` overrides. The example above uses the `default` preset.

| Field | Description |
|:---|:---|
| `messages` | Array of exactly 3 ChatML messages: system, user, assistant. |
| `provenance` | Source repository name and URL. Present on every record. |
| `legal` | License identifier for the record's source code. Present on every record. |
| `originalAssistantMessage` | The original commit message preserved alongside an LLM-generated assistant message. Present **only** when `--llm-assistant-output` is enabled; absent otherwise. |

See [GLOSSARY.md](./GLOSSARY.md) for canonical definitions of Training Example, Provenance, Legal Metadata, and Original Assistant Message.

## Configuration & Requirements

### System Requirements
* **Git**: The `git` binary must be installed and available in your system's `PATH`.
* **Runtime**: .NET 10 SDK is required for building and running the CLI. If running the CLI as a dotnet tool, only the .NET runtime is required.

### LLM Setup for License Detection
To enable automatic license detection, you must provide an AI model configuration via CLI arguments (or the equivalent `DIFFTOJSON_*` environment variables, see below):
* `--model-id`: The ID of the AI model to use (Required if `--license` is not provided).
* `--endpoint-url`: The endpoint URL of the API (Required if `--license` is not provided. Not required by the client itself for `openai`, `anthropic`, or `ollama-cloud`, but the CLI currently requires it whenever `--license` is omitted).
* `--provider`: The AI provider ID (e.g., `ollama`). If not specified (or unknown), it defaults to OpenAI compatible provider mode (`openai-compatible`).
* `--api-key`: The API key for the provider (Required for most providers; not required for local `ollama`; optional for `ollama-cloud` — only sent as an `Authorization: Bearer` header when provided).

#### Environment variable fallback

To avoid passing secrets on the command line (where they are visible in process listings), each of these flags falls back to an environment variable when the flag is empty:

| Flag | Environment variable |
|:---|:---|
| `--provider` | `DIFFTOJSON_PROVIDER` |
| `--api-key` | `DIFFTOJSON_API_KEY` |
| `--endpoint-url` | `DIFFTOJSON_ENDPOINT` |
| `--model-id` | `DIFFTOJSON_MODEL` |

Explicit CLI flags take precedence over environment variables.

#### Supported Providers:

| AI Provider       | Endpoint Type     | Supporting NuGet Package           | CLI Provider Id to use | Notes                                                                                                                      |
|-------------------|-------------------|------------------------------------|------------------------|----------------------------------------------------------------------------------------------------------------------------|
| Ollama            | OpenAI Compatible | ``OllamaSharp``                    | ``ollama``             | Compatible wth Ollama Local and Ollama Cloud - Provide the desired Ollama endpoint URL. No API key required for local Ollama. | 
| Ollama Cloud        | OpenAI Compatible | ``OllamaSharp``                    | ``ollama-cloud``             | API key is optional; when provided it is sent as an `Authorization: Bearer` header. No endpoint URL required (uses `https://ollama.com`). | 
| OpenAI | OpenAI | ``Microsoft.Extensions.AI.OpenAI`` | ``openai``                   |  API Key is required. No endpoint URL required.                                                         |
| OpenAI Compatible | OpenAI Compatible | ``Microsoft.Extensions.AI.OpenAI`` | ``openai-compatible`` (also the default when `--provider` is omitted or unrecognised) | Endpoint URL is required. API Key may be required by the provider.                                                         |
| Anthropic | Anthropic Compatible | ``Anthropic`` | ``anthropic``                    |  API Key is required. No endpoint URL required.                                                         |
| Anthropic Compatible | Anthropic Compatible | ``Anthropic`` | ``anthropic-compatible``                    | Endpoint URL is required. API Key may be required by the provider.                                                         |

Alternatively, you can manually provide the license name using the `--license` flag to skip the LLM call.

**Note**: Provider Ids are case-insensitive.

## Installation

### As a .NET Tool
If you have the .NET 10 Runtime installed, you can install the CLI as a dotnet tool from the NuGet Gallery.

To install it, use:
```bash
dotnet tool install -g DiffToJson
```

To update it use:
```bash
dotnet tool update -g DiffToJson
```

To uninstall it, use:
```bash
dotnet tool uninstall -g DiffToJson
```

## Quick Start

### Without LLM - Specified license Name
```bash
diff-to-json --repo-directory "C:\path\to\your\repo" --license "[LICENSE_NAME]" -o "C:\output\folder"
```

### OpenAI Compatible
```bash
diff-to-json --repo-directory "C:\path\to\your\repo" --model-id "[MODEL_NAME]" --endpoint-url "[OPENAI_COMPATIBLE_ENDPOINT]" --api-key "your-api-key" -o "C:\output\folder"
```

### Ollama (Local)
You can substitute the model id for any of [Ollama's Supported Models](https://ollama.com/search)

```bash
diff-to-json --repo-directory "C:\path\to\your\repo" --model-id "qwen3.5:4b" --endpoint-url "http://localhost:11434" --provider "ollama" -o "C:\output\folder"
```
**Note**: The CLI does not automatically pull the AI model; it must exist on your device at the time the CLI calls the Ollama API endpoint.

### Ollama Cloud
You can substitute the model id for any of [Ollama's Cloud Supported Models](https://ollama.com/search?c=cloud)

#### Ollama Cloud Models via Ollama CLI
```bash
diff-to-json --repo-directory "C:\path\to\your\repo" --model-id "gemma4:31b-cloud" --endpoint-url "http://localhost:11434" --api-key "your-api-key" --provider "ollama" -o "C:\output\folder"
```

#### Ollama Cloud API
```bash
diff-to-json --repo-directory "C:\path\to\your\repo" --model-id "gemma4:31b-cloud" --api-key "your-api-key" --provider "ollama-cloud" -o "C:\output\folder"
```

This will analyse the specified repository and create a file named `{repo-name}-commits.jsonl` inside the specified output folder.

### Training Format with Conventional Commits Preset

```bash
diff-to-json --repo-directory "C:\path\to\your\repo" --format training --prompt-style conventional -o "C:\output\folder"
```

### With LLM-Generated Assistant Messages

```bash
diff-to-json --repo-directory "C:\path\to\your\repo" --format training --llm-assistant-output --model-id "qwen3.5:4b" --endpoint-url "http://localhost:11434" --provider "ollama" -o "C:\output\folder"
```

### With Reasoning Effort Control

```bash
diff-to-json --repo-directory "C:\path\to\your\repo" --format training --llm-assistant-output --reasoning-effort "high" --model-id "qwen3.5:4b" --endpoint-url "http://localhost:11434" --provider "ollama" -o "C:\output\folder"
```

### Using Custom Prompt Overrides

```bash
diff-to-json --repo-directory "C:\path\to\your\repo" --format training --system-prompt "You are an expert Git user." --user-prompt "Summarize this diff for {repoName}: {diff}" -o "C:\output\folder"
```

### Raw (Legacy) Format

```bash
diff-to-json --repo-directory "C:\path\to\your\repo" --format raw -o "C:\output\folder"
```

## CLI Parameters

| Parameter Name | Type | Optional/Required | Default | Notes |
|:---|:---|:---|:---|:---|
| `--repo-directory` | `DirectoryInfo` | Optional | Current directory | The local git repository directory to analyze. |
| `--repo-url` | `string` | Optional | `""` | The URL of the git repository to include in the JSONL output. |
| `--model-id` | `string` | Conditional | `""` | Required if `--license` is not provided. The ID of the AI model to use. Falls back to `DIFFTOJSON_MODEL`. |
| ``--endpoint-url`` | `string` | Conditional | `""` | Required if `--license` is not provided. Falls back to `DIFFTOJSON_ENDPOINT`. Provider clients for `openai`, `anthropic`, and `ollama-cloud` do not need it, but the CLI currently still requires it on the license-detection path. |
| `--api-key` | `string` | Optional | `""` | The API key for the AI provider. Falls back to `DIFFTOJSON_API_KEY`. Not required for local `ollama`; optional for `ollama-cloud`. |
| `--provider` | `string` | Optional | `""` (defaults to `openai-compatible` behavior) | The AI provider ID. Falls back to `DIFFTOJSON_PROVIDER`. See [LLM Setup](#llm-setup-for-license-detection). |
| `--license` | `string` | Optional | `""` | Manually specify the license name. Skips LLM license detection. |
| `--output` / `-o` | `string` | Optional | `{repoDir}/{repoName}-commits.jsonl` | The output file path. If omitted, writes `{repoDir}/{repoName}-commits.jsonl`. If the value ends in `.jsonl` it is treated as a file path (relative paths resolved against the current directory); otherwise it is treated as a directory and `{repoName}-commits.jsonl` is appended. |
| `--format` | `string` | Optional | `training` | Output format. `training` for camelCase ChatML JSONL; `raw` for legacy PascalCase JSONL. |
| `--prompt-style` | `string` | Optional | `default` | Prompt preset name. See [Prompt Presets](#prompt-presets). |
| `--system-prompt` | `string` | Optional | `""` (uses preset) | Override the system prompt template. Supports [placeholders](#placeholders). |
| `--user-prompt` | `string` | Optional | `""` (uses preset) | Override the user prompt template. Supports [placeholders](#placeholders). |
| `--llm-assistant-output` | `bool` | Optional | `false` | Enable LLM-generated assistant messages. Requires `--format training`. See [LLM Override](#llm-override). |
| `--llm-override-prompt` | `string` | Optional | `""` (uses user prompt) | Override the prompt sent to the LLM when `--llm-assistant-output` is enabled. Supports [placeholders](#placeholders). Requires `--llm-assistant-output`. |
| `--reasoning-effort` | `string` | Optional | `auto` | Reasoning effort level for the AI model. Valid values: `auto`, `on`, `off`, `low`, `medium`, `high`, `xhigh`, `max`. The accepted subset depends on the model (see [Reasoning Effort](#reasoning-effort)). Non-`auto` values require `--llm-assistant-output`; using it with `--llm-assistant-output` also requires `--model-id`. |
| `--redaction` | `string` | Optional | `message` | PII redaction tier. `message` redacts only commit messages; `diff` redacts only diffs; `all` redacts both; `none` disables redaction. See [Redaction Tiers](#redaction-tiers). |

## Cross-Option Rules

The following validators enforce constraints between flags:

| Condition | Outcome | Message |
|:---|:---|:---|
| `--llm-assistant-output` + `--format raw` | **Error** — incompatible | `Error: --llm-assistant-output is not compatible with --format raw.` |
| `--llm-override-prompt` set without `--llm-assistant-output` | **Error** — override prompt requires override enabled | `Error: --llm-override-prompt requires --llm-assistant-output.` |
| `--redaction none` + `--llm-assistant-output` | **Warning** — proceeds but may expose PII in LLM output | `Warning: --redaction none combined with --llm-assistant-output may expose PII in LLM output.` |
| `--reasoning-effort` set to non-`auto` without `--llm-assistant-output` | **Error** — reasoning effort requires LLM output enabled | `Error: --reasoning-effort requires --llm-assistant-output when set to a value other than 'auto'.` |
| `--reasoning-effort` set without `--model-id` (with `--llm-assistant-output`) | **Error** — model ID required for reasoning effort | `Error: --reasoning-effort requires --model-id when --llm-assistant-output is enabled.` |
| `--reasoning-effort` set to an unknown value | **Error** — invalid value; CLI lists the values supported for the given model | `Error: --reasoning-effort '<value>' is not a valid value.` + `Supported values: ...` |
| `--reasoning-effort` set to a value the model does not support | **Error** — model-specific rejection; CLI lists the supported values | `Error: --reasoning-effort '<value>' is not supported for provider '<provider>', model '<model>'.` + `Supported values: ...` |

Unknown placeholders in `--system-prompt`, `--user-prompt`, or `--llm-override-prompt` also cause an error before any records are written.

## Prompt Presets

Available via `--prompt-style`. Each preset provides a system and user message template. Placeholders (see below) are substituted at serialization time.

| Preset Name | System Prompt | User Prompt |
|:---|:---|:---|
| `default` | `You are a software engineer. You write high-quality commit messages that follow best practices.` | `Write a commit message for the diff in the repository '{repoName}':\n{diff}` |
| `conventional` | `You are a software engineer. You write commit messages that follow the Conventional Commits specification.` | `Write a Conventional Commits-style commit message for the diff in '{repoName}':\n{diff}` |

Overrides take precedence over the selected preset: provide `--system-prompt` or `--user-prompt` to replace the respective message entirely.

### Placeholders

Placeholder tokens in prompt templates are replaced with record-specific data at serialization time. Unknown placeholders cause a CLI error. The built-in presets only use `{repoName}` and `{diff}`, but overrides (`--system-prompt`, `--user-prompt`, `--llm-override-prompt`) may use any of the following:

| Placeholder | Substituted With |
|:---|:---|
| `{diff}` | The git diff content |
| `{commitMessage}` | The commit message |
| `{repoName}` | The repository name (directory name) |
| `{license}` | The detected or manually specified license |
| `{repoUrl}` | The repository URL from `--repo-url` |

## Redaction Tiers

Available via `--redaction`. Controls which stored fields are passed through the PII redactor (regex-based email redaction) before emission. Redaction is applied to the commit before prompt substitution, so the selected tier also determines what the system/user prompts contain.

| Tier | CLI Value | Commit Message | Diff |
|:---|:---|:---:|:---:|
| None | `none` | — | — |
| Message (default) | `message` | Redacted | — |
| Diff | `diff` | — | Redacted |
| All | `all` | Redacted | Redacted |

Note: LLM-generated assistant text is always passed through the email redactor after generation, regardless of `--redaction`. The `--redaction none` + `--llm-assistant-output` warning still applies because the unredacted diff/message is sent to the external LLM in the request.

## LLM Override

When `--llm-assistant-output` is enabled, the assistant message of each Training Example is generated by an LLM at extraction time, rather than taken from the original commit message. The original message is preserved in `originalAssistantMessage` for downstream evaluation.

- Requires `--format training` (see [Cross-Option Rules](#cross-option-rules)).
- Requires AI provider configuration (`--provider`, `--model-id`, `--endpoint-url`, `--api-key`, or their `DIFFTOJSON_*` environment variable equivalents; per-provider requirements apply).
- Use `--llm-override-prompt` to send a different prompt to the LLM than what appears in the user message.
- The LLM is called with up to 2 retries (1s exponential backoff). Reasoning traces, when present, are embedded as `<think>...</think>` ahead of the visible text.
- On persistent LLM failure (exception, or no assistant message in the response), the record falls back to the (redacted) original commit message for `assistant.content`, with `originalAssistantMessage` populated.
- LLM-generated text is passed through the email redactor after generation regardless of `--redaction`.

## Reasoning Effort

`--reasoning-effort` accepts `auto` (default), `on`, `off`, `low`, `medium`, `high`, `xhigh`, `max`, but the accepted subset is validated per model. Unknown models only accept `auto`. Examples of known model families:

- Full graduated set (`auto` + `on`/`off` + `low`/`medium`/`high`/`xhigh`/`max`): `gpt-4o`, `gpt-4.1*`, `gpt-5*` (non-chat), `claude-sonnet-4.6`/`5`, `claude-opus-4.6+`, `claude-mythos-5`, `claude-fable-5`, `qwen3.5*`.
- Budget-based, no `xhigh`/`max`: `claude-opus-4-5`, `claude-sonnet-4-5`, `claude-haiku-4-5`.
- Graduated without `xhigh` (allows `max`): `deepseek-v4*`.
- Binary thinking (`auto`/`on`/`off` only): `deepseek-v3.1`, `qwen3*`, `minimax-m*`.
- Off only (`auto`/`off`): `deepseek-chat`, `deepseek-v3`.

If the value is invalid or unsupported for the given `--model-id`, the CLI exits with an error listing the supported values (see [Cross-Option Rules](#cross-option-rules)). `auto` leaves the default behavior (including default `MaxOutputTokens` of 16,000 when the model produces reasoning on `auto`, else 8,000).

## How to Build

### Standard Build
Build the project using the .NET CLI:
```bash
dotnet build src/DiffToJsonCli/DiffToJsonCli.csproj
```

### Running the Tool
You can run the tool directly from the source:
```bash
dotnet run --project src/DiffToJsonCli/DiffToJsonCli.csproj -- [args]
```

### Publishing (Native AOT)
For high-performance execution and a standalone binary without requiring the .NET runtime, publish as Native AOT:
```bash
dotnet publish -c Release -r [runtime-identifier] -p:PublishAoT=true
```
Replace `[runtime-identifier]` with the appropriate RID for your platform (e.g., `win-x64`, `linux-x64`, `osx-arm64`).

## Technical Details

### PII Redaction
The tool uses a regex-based approach to detect and redact email addresses (bare addresses and `<name@domain>` forms, replaced with `REDACTED`) within commit messages and/or diffs, depending on the `--redaction` tier, to help prevent the leaking of personally identifiable information (PII). LLM-generated assistant text is additionally always redacted. Due to the nature of regex, this is a best-effort implementation and does not guarantee 100% redaction.
For sensitive git email addresses, always conduct a human review. 

### License Detection Logic
The tool automatically discovers license information by searching for `LICENSE.md`, `LICENSE.txt`, or `LICENSE` files in the repository root (in that priority order). If found, the content is sent to a configured LLM (via the configured AI provider) to extract the license name. If no file is found or the LLM cannot determine the license, it falls back to "Unknown". Provide `--license` to skip the LLM call entirely.

### Merge Commits

Merge commits are omitted from the output. The tool retrieves diffs via `git log -p`, which by default produces no diff output for merge commits. The parser skips any commit with an empty diff body, so merge commits are excluded regardless of format.

### Native AOT Compatibility
The application is designed for Native AOT compatibility, ensuring fast startup times and a small deployment footprint.

## Roadmap
These are some things I'd like to work towards in future versions but are not guaranteed to appear in future versions.

In no particular order:
* AWS Bedrock support

## Star History

<a href="https://www.star-history.com/?repos=alastairlundy%2FDiffToJson&type=date&logscale=&legend=top-left">
 <picture>
   <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/chart?repos=alastairlundy/DiffToJson&type=date&theme=dark&logscale&legend=top-left" />
   <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/chart?repos=alastairlundy/DiffToJson&type=date&logscale&legend=top-left" />
   <img alt="Star History Chart" src="https://api.star-history.com/chart?repos=alastairlundy/DiffToJson&type=date&logscale&legend=top-left" />
 </picture>
</a>

## License
This project contains AI-generated code and human-written code. All human written code in this project is licensed under the Apache 2.0 license.
