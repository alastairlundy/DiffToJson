# GitDiffToJsonL

A CLI for detecting and serializing Git commit Diffs and commit messages from a local Git repository to a JSONL file.

This can be useful for preparing git commit diffs and message data for training AI/ML models or similar use cases.

**NOTE**: Whilst the CLI implements a Regex pattern matching based PII detector for detecting email addresses in Commit Messages and redacting them, redaction of email addresses is not guaranteed. If commit messages contain sensitive information conduct a human review theoutput JSONL file.

## Documented Information
The following is provided in the output JSONL files:
* The Git Diff
* The Git Commit Message associated with the diff
* The License Name if a LICENSE.txt, LICENSE.md, or LICENSE.txt file is present in the repo directory - An LLM call is required to compute this. As a fallback "Unknown" is returned otherwise.
* The Git project name - Obtained from the Git Repo Directory name
* The Git Repo URL if provided by the CLI called.

## Configuration & Requirements

### System Requirements
* **Git**: The `git` binary must be installed and available in your system's `PATH`.
* **Runtime**: .NET 10 SDK is required for building and running the CLI. If running the CLI as a dotnet tool only the .NET runtime is required.

### LLM Setup for License Detection
To enable automatic license detection, you must provide an AI model configuration via CLI arguments:
* `--model-id`: The ID of the AI model to use (Required if `--license` is not provided).
* `--endpoint-url`: The endpoint URL of the OpenAI-compatible API (Required if `--license` is not provided).
* `--provider`: The AI provider ID (e.g., `ollama`). If not specified, it defaults to OpenAI compatible provider mode.
* `--api-key`: The API key for the provider (Required unless using `ollama`).

#### Supported Providers:

| AI Provider | Endpoint Type | Supporting NuGet Package | CLI Provider Id to use | Notes |
|-|-|-|-|-|
| Ollama | OpenAI Compatible | ``OllamaSharp`` | ``ollama`` | Compatible wth Ollama Local and Ollama Cloud - Provide the desired Ollama endpoint URL. API Key required for Ollama Cloud. | 
| OpenAI Compatible | OpenAI Compatible | ``Microsoft.Extensions.AI.OpenAI`` | N/A | Endpoint URL is required. API Key may be required by the provider. |

Alternatively, you can manually provide the license name using the `--license` flag to skip the LLM call.

**Note**: Provider Ids are case insensitive.

## How to Build

### Standard Build
Build the project using the .NET CLI:
```bash
dotnet build src/GitDiffToJsonLCli/GitDiffToJsonLCli.csproj
```

### Running the Tool
You can run the tool directly from the source:
```bash
dotnet run --project src/GitDiffToJsonLCli/GitDiffToJsonLCli.csproj -- [args]
```

### Publishing (Native AOT)
For high-performance execution and a standalone binary without requiring the .NET runtime, publish as Native AOT:
```bash
dotnet publish -c Release -r [runtime-identifier] -p:PublishAoT=true
```
Replace `[runtime-identifier]` with the appropriate RID for your platform (e.g., `win-x64`, `linux-x64`, `osx-arm64`).

## Technical Details

### PII Redaction
The tool employs a regex-based approach to detect and redact email addresses within commit messages to help prevent the leakage of personally identifiable information (PII). Due to the nature of regex, this is a best-effort implementation and does not guarantee 100% redaction.

### License Detection Logic
The tool automatically discovers license information by searching for `LICENSE.md`, `LICENSE.txt`, or `LICENSE` files in the repository root. If found, the content is sent to a configured LLM (via `OllamaSharp` or `Microsoft.Extensions.AI.OpenAI`) to extract the license name. If no file is found or the LLM cannot determine the license, it falls back to "Unknown".

### Native AOT Compatibility
The application is designed for Native AOT compatibility, ensuring fast startup times and a small deployment footprint.

## Roadmap
These are some things I'd like to work towards in future versions but are not guaranteed to appear in future versions.

In no particular order:
* Anthropic support
* Anthropic Compatible Endpoint support
* AWS Bedrock support
* Support for specifying the name of the output file
* Support for working with ``Microsoft.Extensions.Compliance.Redaction`` to enable support for different implementations and types of PII redaction.
* Support for disabling PII redaction

## License
This project contains AI generated code and human written code. All human written code in this project is licensed under the Apache 2.0 license.
