# DiffToJson

[![Latest NuGet Version](https://img.shields.io/nuget/v/DiffToJson?style=flat-square)](https://nuget.org/packages/DiffToJson)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DiffToJson?style=flat-square)](https://nuget.org/packages/DiffToJson)
![GitHub License](https://img.shields.io/github/license/alastairlundy/DiffToJson?style=flat-square)
![OpenSSF Scorecard Score](https://img.shields.io/ossf-scorecard/github.com/alastairlundy/DiffToJson?style=flat-square&label=OpenSSF%20Scorecard%20Score)


A CLI for detecting and serializing Git commit Diffs and commit messages from a local Git repository to a .JSONL file.

This can be useful for preparing git commit diffs and message data for training AI/ML models or similar use cases.

**NOTE**: Whilst the CLI implements a Regex pattern matching based PII detector for detecting email addresses in Commit Messages and redacting them, redaction of email addresses is not guaranteed. If commit messages contain sensitive information, conduct a human review of the output .JSONL file.

## Documented Information
The following is provided in output .JSONL files:
* The Git Diff
* The Git Commit Message associated with the diff
* The license Name if a LICENSE.txt, LICENSE.md, or LICENSE.txt file is present in the repo directory – An LLM call is required to compute this. As a fallback "Unknown" is returned otherwise.
* The Git project name – Obtained from the Git Repo Directory name
* The Git Repo URL if provided by the CLI caller.

## Configuration & Requirements

### System Requirements
* **Git**: The `git` binary must be installed and available in your system's `PATH`.
* **Runtime**: .NET 10 SDK is required for building and running the CLI. If running the CLI as a dotnet tool, only the .NET runtime is required.

### LLM Setup for License Detection
To enable automatic license detection, you must provide an AI model configuration via CLI arguments:
* `--model-id`: The ID of the AI model to use (Required if `--license` is not provided).
* `--endpoint-url`: The endpoint URL of the OpenAI-compatible API (Required if `--license` is not provided).
* `--provider`: The AI provider ID (e.g., `ollama`). If not specified, it defaults to OpenAI compatible provider mode.
* `--api-key`: The API key for the provider (Required unless using `ollama`).

#### Supported Providers:

| AI Provider       | Endpoint Type     | Supporting NuGet Package           | CLI Provider Id to use | Notes                                                                                                                      |
|-------------------|-------------------|------------------------------------|------------------------|----------------------------------------------------------------------------------------------------------------------------|
| Ollama            | OpenAI Compatible | ``OllamaSharp``                    | ``ollama``             | Compatible wth Ollama Local and Ollama Cloud - Provide the desired Ollama endpoint URL. API Key required for Ollama Cloud. | 
| Ollama Cloud        | OpenAI Compatible | ``OllamaSharp``                    | ``ollama-cloud``             | API Key is required. | 
| OpenAI | OpenAI | ``Microsoft.Extensions.AI.OpenAI`` | ``openai``                   |  API Key is required.                                                         |
| OpenAI Compatible | OpenAI Compatible | ``Microsoft.Extensions.AI.OpenAI`` | N/A                    | Endpoint URL is required. API Key may be required by the provider.                                                         |
| Anthropic | Anthropic Compatible | ``Anthropic`` | ``anthropic``                    |  API Key is required.                                                         |
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

## CLI Parameters

| Parameter Name         | Type            | Optional/Required | Default Value             | Notes                                                                                   |
|:-----------------------|:----------------|:------------------|:--------------------------|:----------------------------------------------------------------------------------------|
| `--repo-directory`     | `DirectoryInfo` | Optional          | Current Working Directory | The local git repository directory to analyze.                                          |
| `--repo-url`           | `string`        | Optional          | None                      | The URL of the git repository to be included in the JSONL output.                       |
| `--model-id`           | `string`        | Conditional       | None                      | Required if `--license` is not provided. The ID of the AI model to use.                 |
| `--endpoint-url`       | `string`        | Optional       | None                      | Required if `--license` is not provided, or if the API provider is not ``openai``, ``ollama-cloud``, or ``anthropic``. The endpoint URL of the API. |
| `--api-key`            | `string`        | Optional          | None                      | The API key for the AI provider.                                                        |
| `--provider`           | `string`        | Optional          | OpenAI Compatible         | The AI provider ID (e.g., `ollama`).                                                    |
| `--license`            | `string`        | Optional          | None                      | Manually specify the license name. If provided, LLM license detection is skipped.       |
| `--output-file` / `-o` | `string`        | Optional          | Repo Directory            | The directory path where the output file will be saved.                                 |

### Potential Surprises

* **Output File Naming**: The `--output-file` (or `-o`) parameter specifies a **directory**, not a full file path. The tool will automatically name the file `{repoName}-commits.jsonl` within that directory. If no output path is provided, it defaults to the repository directory.

## How to Build

### Standard Build
Build the project using the .NET CLI:
```bash
dotnet build src/GitDiffToJsonCli/GitDiffToJsonCli.csproj
```

### Running the Tool
You can run the tool directly from the source:
```bash
dotnet run --project src/GitDiffToJsonCli/GitDiffToJsonCli.csproj -- [args]
```

### Publishing (Native AOT)
For high-performance execution and a standalone binary without requiring the .NET runtime, publish as Native AOT:
```bash
dotnet publish -c Release -r [runtime-identifier] -p:PublishAoT=true
```
Replace `[runtime-identifier]` with the appropriate RID for your platform (e.g., `win-x64`, `linux-x64`, `osx-arm64`).

## Technical Details

### PII Redaction
The tool uses a regex-based approach to detect and redact email addresses within commit messages to help prevent the leaking of personally identifiable information (PII). Due to the nature of regex, this is a best-effort implementation and does not guarantee 100% redaction.
For sensitive git email addresses, always conduct a human review. 

### License Detection Logic
The tool automatically discovers license information by searching for `LICENSE.md`, `LICENSE.txt`, or `LICENSE` files in the repository root. If found, the content is sent to a configured LLM (via `OllamaSharp` or `Microsoft.Extensions.AI.OpenAI`) to extract the license name. If no file is found or the LLM cannot determine the license, it falls back to "Unknown".

### Native AOT Compatibility
The application is designed for Native AOT compatibility, ensuring fast startup times and a small deployment footprint.

## Roadmap
These are some things I'd like to work towards in future versions but are not guaranteed to appear in future versions.

In no particular order:
* AWS Bedrock support
* Support for specifying the name of the output file
* Support for working with ``Microsoft.Extensions.Compliance.Redaction`` to enable support for different implementations and types of PII redaction.
* Support for disabling PII redaction

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
