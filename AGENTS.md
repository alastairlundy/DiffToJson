# AGENTS.md

## Project Overview
A .NET 10 CLI tool that serializes Git commit diffs and messages into JSONL format, specifically for AI/ML training data preparation.

## Technical Stack
- **Framework**: .NET 10.0
- **Key Libraries**:
  - `System.CommandLine`: CLI argument parsing.
  - `CliInvoke`: Execution of Git commands.
  - `OllamaSharp`, `Microsoft.Extensions.AI.OpenAI` & `Anthropic`: AI/LLM provider implementations.
  - `Microsoft.Extensions.AI.Abstractions`: Provider-agnostic AI interfaces.

## Development Commands
- **Build**: `dotnet build src/DiffToJsonCli/DiffToJsonCli.csproj`
- **Run**: `dotnet run --project src/DiffToJsonCli/DiffToJsonCli.csproj -- [args]`
- **Publish (Native AOT)**: `dotnet publish -c Release -r [runtime-identifier] -p:PublishAoT=true` - Valid runtime identifiers include the OS part ("win" for Windows, "linux" for Linux, "osx" for macOS), a "-" separator, and the CPU architecture part ("x64" for x86-64 CPUs, "arm64" for ARM64 CPUs). E.g. `win-x64` or `osx-arm64`. 

## Structure
- `src/DiffToJsonLib/`: Reusable business logic, including Git data extraction, PII redaction, AI-powered license detection (provider-agnostic), and JSONL serialization/writing.
- `src/DiffToJsonCli/`: Main application logic. Handles CLI input parsing, provider-specific AI connections, and overall operation orchestration.
- `src/DiffToJsonCli/Contexts/`: JSON serialization contexts.
- `src/DiffToJsonApp.slnx`: Modern .NET solution file.

## Contribution Guidelines
All contributions must follow the processes and quality standards defined in [CONTRIBUTING.md](./CONTRIBUTING.md), including strict adherence to the [AI_POLICY.md](./AI_POLICY.md) for all AI-assisted work.

## Important Constraints
- **PII Redaction**: Performed within the library's data extraction process; however, regex-based redaction is not guaranteed to be 100% successful. Human review is required for sensitive data.
- **License Detection**: Optional feature. If enabled, the CLI provides an AI client to the library's `LicenseDetector` to determine the license from `LICENSE*` files, falling back to "Unknown".

## Agent skills

### Issue tracker

Issues and PRDs live as GitHub issues. See `docs/agents/issue-tracker.md`.

### Triage labels

Standard triage labels (e.g., `needs-triage`, `ready-for-agent`). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context layout. See `docs/agents/domain.md`.

