/*
    Copyright 2026 Alastair Lundy

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
 */

using System.CommandLine;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using CliInvoke;
using CliInvoke.Core;
using DiffToJsonLib.Prompts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Compliance.Redaction;
using DiffToJsonLib.Redactors;

HashSet<string> knownPlaceholders = new(StringComparer.OrdinalIgnoreCase)
{
    "diff", "commitMessage", "repoName", "license", "repoUrl"
};

Regex placeholderPattern = new(@"\{(\w+)\}", RegexOptions.Compiled);

string? ValidatePlaceholders(string value)
{
    if (string.IsNullOrEmpty(value)) return null;

    MatchCollection matches = placeholderPattern.Matches(value);
    foreach (Match match in matches)
    {
        string name = match.Groups[1].Value;
        if (!knownPlaceholders.Contains(name))
        {
            return $"Unknown placeholder '{{{name}}}'. Valid placeholders: {string.Join(", ", knownPlaceholders.Select(p => $"{{{p}}}"))}.";
        }
    }

    return null;
}

static string SubstitutePlaceholders(string text, string diff, string commitMessage,
    string repoName, string license, string repoUrl)
{
    return text
        .Replace("{diff}", diff)
        .Replace("{commitMessage}", commitMessage)
        .Replace("{repoName}", repoName)
        .Replace("{license}", license)
        .Replace("{repoUrl}", repoUrl);
}

IServiceCollection services = new ServiceCollection();

services.AddSingleton<IProcessInvoker, ProcessInvoker>();
services.AddRedaction(redaction =>
{
    redaction.SetFallbackRedactor<RegexPiiRedactor>();
});

services.AddSingleton<IDiffJsonFileWriter, DiffJsonFileWriter>();
services.AddSingleton<IDiffTrainingJsonFileWriter, DiffTrainingJsonFileWriter>();
services.AddSingleton<IGitCommitParser, GitCommitParser>();

Option<DirectoryInfo> repoDirectoryOption = new("--repo-directory")
{
    Description = "The local git repository directory. Falls back to the current directory if not provided.",
    DefaultValueFactory = _ => new DirectoryInfo(Directory.GetCurrentDirectory()),
    Required = false
};

Option<string> repoUrlOption = new("--repo-url")
{
    Description = "The URL of the git repository.",
    DefaultValueFactory = _ => "",
    Required = false
};

Option<string> modelIdOption = new("--model-id")
{
    Description = "The model id of the AI model to use",
    DefaultValueFactory = _ => "",
    Required = false
};

Option<string> endpointUrlOption = new("--endpoint-url")
{
    Description = "The endpoint URL of the OpenAI compatible API endpoint to use.",
    DefaultValueFactory = _ => "",
    Required = false
};

Option<string> apiKeyOption = new("--api-key")
{
    Description = "The API key of the AI provider to use.",
    DefaultValueFactory = _ => "",
    Required = false,
};

Option<string> providerOption = new("--provider")
{
    Description = "The Id of the AI provider to use.",
    DefaultValueFactory = _ => "",
    Required = false,
};

Option<string> licenseOption = new("--license")
{
    Description = "The licence name to use for the JSON.",
    Required = false,
    DefaultValueFactory = _ => ""
};

Option<string> outputFilePathOption = new("--output-file", ["-o"])
{
    Description = "The output file path. If not specified, the default is the repository directory path.",
    Required = false,
    DefaultValueFactory = _ => ""
};

Option<string> formatOption = new("--format")
{
    Description = "The output format. 'training' produces camelCase JSONL for AI training; 'raw' produces the legacy PascalCase JSONL.",
    DefaultValueFactory = _ => "training"
};
formatOption.AcceptOnlyFromAmong("training", "raw");

Option<string> promptStyleOption = new("--prompt-style")
{
    Description = "The prompt preset to use for training records.",
    DefaultValueFactory = _ => "default"
};
promptStyleOption.AcceptOnlyFromAmong("default", "conventional");

Option<string> systemPromptOption = new("--system-prompt")
{
    Description = "Override the system prompt template. Supports placeholders: {diff}, {commitMessage}, {repoName}, {license}, {repoUrl}.",
    DefaultValueFactory = _ => ""
};

Option<string> userPromptOption = new("--user-prompt")
{
    Description = "Override the user prompt template. Supports placeholders: {diff}, {commitMessage}, {repoName}, {license}, {repoUrl}.",
    DefaultValueFactory = _ => ""
};

Option<bool> llmAssistantOutputOption = new("--llm-assistant-output")
{
    Description = "Enable LLM-generated assistant messages for each commit. Requires --provider, --model-id, --api-key, and --endpoint-url.",
    DefaultValueFactory = _ => false
};

Option<string> llmOverridePromptOption = new("--llm-override-prompt")
{
    Description = "Override the user prompt sent to the LLM when --llm-assistant-output is enabled. Supports placeholders.",
    DefaultValueFactory = _ => ""
};

Option<string> redactionOption = new("--redaction")
{
    Description = "PII redaction tier for training records. 'none' disables redaction; 'message' redacts only commit messages; 'diff' redacts only diffs; 'all' redacts both.",
    DefaultValueFactory = _ => "message"
};
redactionOption.AcceptOnlyFromAmong("message", "diff", "all", "none");

RootCommand rootCommand = new("Detects and Serializes Git Diffs and Commits to a .JSONL file.")
{
    repoDirectoryOption,
    repoUrlOption,
    modelIdOption,
    endpointUrlOption,
    providerOption,
    apiKeyOption,
    licenseOption,
    outputFilePathOption,
    formatOption,
    promptStyleOption,
    systemPromptOption,
    userPromptOption,
    llmAssistantOutputOption,
    llmOverridePromptOption,
    redactionOption
};

rootCommand.SetAction(async result =>
{
    try
    {
        DirectoryInfo targetDir = result.GetValue(repoDirectoryOption) ?? new DirectoryInfo(Directory.GetCurrentDirectory());
        string repoUrl = result.GetValue(repoUrlOption) ?? "";
        string repoName = targetDir.Name;

        string outputFilePath = result.GetValue(outputFilePathOption) ?? "";

        string outputPath;
        if (string.IsNullOrEmpty(outputFilePath))
        {
            outputPath = $"{targetDir.FullName}{Path.DirectorySeparatorChar}{repoName}-commits.jsonl";
        }
        else
        {
            DirectoryInfo directoryInfo = new(outputFilePath);

            outputPath = outputFilePath.EndsWith(".jsonl") ?
                Path.Combine(directoryInfo.FullName, outputFilePath) :
                Path.Combine(directoryInfo.FullName, $"{repoName}-commits.jsonl");
        }

        string provider = result.GetValue(providerOption) ?? "";
        string apiKey = result.GetValue(apiKeyOption) ?? "";
        string? endpointUrl = result.GetValue(endpointUrlOption);
        string? modelId = result.GetValue(modelIdOption);

        string licenseProvided = result.GetValue(licenseOption) ?? "";

        string format = result.GetValue(formatOption) ?? "training";
        string promptStyle = result.GetValue(promptStyleOption) ?? "default";
        string systemPromptOverride = result.GetValue(systemPromptOption) ?? "";
        string userPromptOverride = result.GetValue(userPromptOption) ?? "";
        bool llmAssistantOutput = result.GetValue(llmAssistantOutputOption);
        string llmOverridePrompt = result.GetValue(llmOverridePromptOption) ?? "";
        string redactionStr = result.GetValue(redactionOption) ?? "message";

        if (llmAssistantOutput && format == "raw")
        {
            await Console.Error.WriteLineAsync("Error: --llm-assistant-output is not compatible with --format raw.");
            Environment.Exit(1);
            return;
        }

        if (!string.IsNullOrEmpty(llmOverridePrompt) && !llmAssistantOutput)
        {
            await Console.Error.WriteLineAsync("Error: --llm-override-prompt requires --llm-assistant-output.");
            Environment.Exit(1);
            return;
        }

        if (redactionStr == "none" && llmAssistantOutput)
        {
            await Console.Out.WriteLineAsync("Warning: --redaction none combined with --llm-assistant-output may expose PII in LLM output.");
        }

        string? validationError;
        foreach (var entry in new[] {
            (Value: systemPromptOverride, Flag: "--system-prompt"),
            (Value: userPromptOverride, Flag: "--user-prompt"),
            (Value: llmOverridePrompt, Flag: "--llm-override-prompt")
        })
        {
            validationError = ValidatePlaceholders(entry.Value);
            if (validationError is not null)
            {
                await Console.Error.WriteLineAsync($"Error in {entry.Flag}: {validationError}");
                Environment.Exit(1);
                return;
            }
        }

        Console.WriteLine($"Analyzing repository: {targetDir.Name} at {targetDir.FullName}");

        RedactionTier redactionTier = redactionStr switch
        {
            "none" => RedactionTier.None,
            "message" => RedactionTier.Message,
            "diff" => RedactionTier.Diff,
            "all" => RedactionTier.All,
            _ => RedactionTier.Message
        };

        IServiceProvider serviceProvider;

        services.AddSingleton<IChatClientFactory>(_ =>
            new ChatClientFactory(provider, apiKey, endpointUrl ?? "", modelId ?? ""));

        services.AddSingleton(sp =>
            new LlmAssistantWriter(sp.GetRequiredService<IChatClientFactory>(), redactionTier));

        string license;

        if (string.IsNullOrEmpty(licenseProvided))
        {
            ArgumentException.ThrowIfNullOrEmpty(endpointUrl);
            ArgumentException.ThrowIfNullOrEmpty(modelId);

            services.AddSingleton<ILicenseAnalyzer, AILicenseAnalyzer>();
            serviceProvider = services.BuildServiceProvider();

            ILicenseAnalyzer licenseAnalyzer = serviceProvider.GetRequiredService<ILicenseAnalyzer>();

            FileInfo? fileInfo = await LicenseFileFinder.FindLicenseFile(targetDir.FullName);

            if (fileInfo is not null)
                license = await licenseAnalyzer.AnalyzeLicenseAsync(fileInfo) ?? "Unknown";
            else
                license = "Unknown";

            await Console.Out.WriteLineAsync($"Detected License: {license}");
        }
        else
        {
            serviceProvider = services.BuildServiceProvider();
            license = licenseProvided;
            await Console.Out.WriteLineAsync($"Using specified License: {license}");
        }

        IGitCommitParser commitParser = serviceProvider.GetRequiredService<IGitCommitParser>();

        if (format == "raw")
        {
            IDiffJsonFileWriter diffJsonFileWriter = serviceProvider.GetRequiredService<IDiffJsonFileWriter>();

            IAsyncEnumerable<CommitRecord> records = commitParser.ParseCommitsStreamAsync(repoName, license,
                targetDir.FullName, repoUrl, CancellationToken.None);

            await diffJsonFileWriter.WriteToJsonFileAsync(records, outputPath, CancellationToken.None);
        }
        else
        {
            IDiffTrainingJsonFileWriter trainingWriter = serviceProvider.GetRequiredService<IDiffTrainingJsonFileWriter>();
            LlmAssistantWriter llmWriter = serviceProvider.GetRequiredService<LlmAssistantWriter>();

            PromptTemplate preset = PromptPresets.Get(promptStyle);
            string effectiveSystemTemplate = !string.IsNullOrEmpty(systemPromptOverride)
                ? systemPromptOverride
                : preset.System;
            string effectiveUserTemplate = !string.IsNullOrEmpty(userPromptOverride)
                ? userPromptOverride
                : preset.User;

            IAsyncEnumerable<CommitRecord> rawCommits = commitParser.ParseCommitsStreamAsync(repoName, license,
                targetDir.FullName, repoUrl, CancellationToken.None);

            IAsyncEnumerable<CommitTrainingRecord> trainingRecords =
                BuildTrainingRecords(rawCommits, effectiveSystemTemplate, effectiveUserTemplate,
                    redactionTier, llmAssistantOutput, llmOverridePrompt,
                    llmWriter, repoName, license, repoUrl, CancellationToken.None);

            await trainingWriter.WriteToJsonFileAsync(trainingRecords, outputPath, CancellationToken.None);
        }

        await Console.Out.WriteLineAsync($"Successfully wrote commits to {outputPath}");
    }
    catch (Exception ex)
    {
        await Console.Error.WriteLineAsync($"Error: {ex.Message}");
        Environment.Exit(1);
    }
});

ParseResult parseResult = rootCommand.Parse(args);

return await parseResult.InvokeAsync();

async IAsyncEnumerable<CommitTrainingRecord> BuildTrainingRecords(
    IAsyncEnumerable<CommitRecord> source,
    string systemTemplate, string userTemplate,
    RedactionTier redactionTier,
    bool llmAssistantOutput, string llmOverridePrompt,
    LlmAssistantWriter llmWriter,
    string repoName, string license, string repoUrl,
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    RegexPiiRedactor piiRedactor = new();

    await foreach (CommitRecord commit in source.WithCancellation(cancellationToken))
    {
        string message = redactionTier >= RedactionTier.Message
            ? piiRedactor.Redact(commit.CommitMessage)
            : commit.CommitMessage;

        string diff = redactionTier >= RedactionTier.Diff
            ? piiRedactor.Redact(commit.Diff)
            : commit.Diff;

        string systemContent = SubstitutePlaceholders(systemTemplate, diff, message,
            commit.RepoName, license, commit.RepoUrl);
        string userContent = SubstitutePlaceholders(userTemplate, diff, message,
            commit.RepoName, license, commit.RepoUrl);

        string? assistantContent;
        string? originalAssistantMessage = null;

        if (llmAssistantOutput)
        {
            string llmUserPrompt = !string.IsNullOrEmpty(llmOverridePrompt)
                ? SubstitutePlaceholders(llmOverridePrompt, diff, message,
                    commit.RepoName, license, commit.RepoUrl)
                : userContent;

            string? llmResult = await llmWriter.GenerateAssistantAsync(
                systemContent, llmUserPrompt,
                commit.RepoName, license, commit.RepoUrl,
                cancellationToken);

            if (llmResult is not null)
            {
                assistantContent = llmResult;
                originalAssistantMessage = message;
            }
            else
            {
                assistantContent = null;
                originalAssistantMessage = message;
            }
        }
        else
        {
            assistantContent = message;
        }

        yield return new CommitTrainingRecord(
            Messages:
            [
                new Message("system", systemContent),
                new Message("user", userContent),
                new Message("assistant", assistantContent)
            ],
            Provenance: new Provenance(commit.RepoName, commit.RepoUrl),
            Legal: new Legal(license),
            OriginalAssistantMessage: originalAssistantMessage
        );
    }
}


