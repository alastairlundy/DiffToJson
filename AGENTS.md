# AGENTS.md

## Project Overview
A .NET 10 CLI tool that serializes Git commit diffs and messages into JSONL format, specifically for AI/ML training data preparation.

## Technical Stack
- **Framework**: .NET 10.0
- **Key Libraries**:
  - `System.CommandLine`: CLI argument parsing.
  - `CliInvoke`: Execution of Git commands.
  - `OllamaSharp` & `Microsoft.Extensions.AI.OpenAI`: AI/LLM integration for license detection.

## Development Commands
- **Build**: `dotnet build src/GitDiffToJsonCli/GitDiffToJsonCli.csproj`
- **Run**: `dotnet run --project src/GitDiffToJsonCli/GitDiffToJsonCli.csproj -- [args]`
- **Publish (Native AOT)**: `dotnet publish -c Release -r [runtime-identifier] -p:PublishAoT=true` - Valid runtime identifiers include the OS part ("win" for Windows, "linux" for Linux, "osx" for macOS), a "-" separator, and the CPU architecture part ("x64" for x86-64 CPUs, "arm64" for ARM64 CPUs). E.g. `win-x64` or `osx-arm64`. 

## Structure
- `src/GitDiffToJsonCli/`: Main application logic.
- `src/GitDiffToJsonCli/Contexts/`: JSON serialization contexts.
- `src/GitDiffToJsonApp.slnx`: Modern .NET solution file.

## Important Constraints
- **PII Redaction**: The tool uses regex to redact email addresses in commit messages, but this is not guaranteed to be successful. Human review is required for sensitive data.
- **License Detection**: Can be provided manually via parameter; otherwise requires an LLM call to determine the license from `LICENSE*` files, falling back to "Unknown".
