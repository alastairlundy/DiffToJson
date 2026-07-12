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
using CliInvoke.Extensions;
using DiffToJsonLib.Prompts;
using DiffToJsonLib.Training;
using DiffToJsonLib.Training.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using DiffToJsonLib.Reasoning;
using DiffToJsonLib.Models;
using DiffToJsonLib.Redactors;
using DiffToJsonLib.Writers;
using Microsoft.Extensions.Compliance.Redaction;
using ReasoningEffort = DiffToJsonLib.Reasoning.ReasoningEffort;

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

IServiceCollection services = new ServiceCollection();

services.AddCliInvoke();
services.AddRedaction(redaction =>
{
    redaction.SetFallbackRedactor<RegexPiiRedactor>();
});

services.AddSingleton<RedactionPolicy>(sp =>
{
    var redactor = sp.GetRequiredService<Redactor>();
    return new RedactionPolicy(new Dictionary<RedactionTier, Redactor>
    {
        [RedactionTier.Message] = redactor,
        [RedactionTier.Diff] = redactor,
        [RedactionTier.All] = redactor
    });
});
services.AddSingleton<IAssistantMessageGenerator, LlmAssistantWriter>();
services.AddSingleton<TrainingExampleBuilder>();
services.AddSingleton<IDiffJsonFileWriter, DiffJsonFileWriter>();
services.AddSingleton<IDiffTrainingJsonFileWriter, DiffTrainingJsonFileWriter>();
services.AddSingleton<IGitCommitParser, GitCommitParser>();
services.AddSingleton<IReasoningEffortMatrix, ReasoningEffortMatrix>();
services.AddSingleton<IChatOptionsBuilder, ChatOptionsBuilder>();

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

Option<string> outputFilePathOption = new("--output", ["-o"])
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

Option<string> reasoningEffortOption = new("--reasoning-effort")
{
    Description = "The reasoning effort level used by the AI model. Valid values: auto, on, off, low, medium, high, xhigh, max. Valid values depend on the active (provider, model).",
    DefaultValueFactory = _ => "auto",
    Required = false
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
    reasoningEffortOption,
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
        string reasoningEffortStr = result.GetValue(reasoningEffortOption) ?? "auto";

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

        if (!string.IsNullOrEmpty(reasoningEffortStr) && !string.Equals(reasoningEffortStr, "auto", StringComparison.OrdinalIgnoreCase) && !llmAssistantOutput)
        {
            await Console.Error.WriteLineAsync("Error: --reasoning-effort requires --llm-assistant-output when set to a value other than 'auto'.");
            Environment.Exit(1);
            return;
        }

        IServiceProvider serviceProvider;

        services.AddSingleton<IChatClientFactory>(_ =>
            new ChatClientFactory(provider, apiKey, endpointUrl ?? "", modelId ?? ""));

        services.AddSingleton(sp => new LlmAssistantWriter(
            sp.GetRequiredService<IChatClientFactory>(),
            sp.GetRequiredService<RedactionPolicy>()));

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

        IReasoningEffortMatrix matrix = serviceProvider.GetRequiredService<IReasoningEffortMatrix>();
        IChatOptionsBuilder chatOptionsBuilder = serviceProvider.GetRequiredService<IChatOptionsBuilder>();

        if (llmAssistantOutput && !string.IsNullOrEmpty(reasoningEffortStr))
        {
            if (string.IsNullOrEmpty(modelId))
            {
                await Console.Error.WriteLineAsync("Error: --reasoning-effort requires --model-id when --llm-assistant-output is enabled.");
                Environment.Exit(1);
                return;
            }

            if (!Enum.TryParse<ReasoningEffort>(reasoningEffortStr, ignoreCase: true, out var reasoningEffort))
            {
                IReadOnlySet<ReasoningEffort> validSet = matrix.GetSupportedReasoningValues(modelId);
                string validList = string.Join(", ", validSet.Select(v => v.ToString().ToLowerInvariant()));
                await Console.Error.WriteLineAsync($"Error: --reasoning-effort '{reasoningEffortStr}' is not a valid value.");
                await Console.Error.WriteLineAsync($"Supported values: {validList}");
                Environment.Exit(1);
                return;
            }

            IReadOnlySet<ReasoningEffort> supported = matrix.GetSupportedReasoningValues(modelId);
            if (!supported.Contains(reasoningEffort))
            {
                string validList = string.Join(", ", supported.Select(v => v.ToString().ToLowerInvariant()));
                await Console.Error.WriteLineAsync($"Error: --reasoning-effort '{reasoningEffortStr}' is not supported for provider '{provider}', model '{modelId}'.");
                await Console.Error.WriteLineAsync($"Supported values: {validList}");
                Environment.Exit(1);
                return;
            }
        }

        IGitCommitParser commitParser = serviceProvider.GetRequiredService<IGitCommitParser>();

        if (format == "raw")
        {
            IDiffJsonFileWriter diffJsonFileWriter = serviceProvider.GetRequiredService<IDiffJsonFileWriter>();
            RedactionPolicy redactionPolicy = serviceProvider.GetRequiredService<RedactionPolicy>();

            static async IAsyncEnumerable<CommitRecord> ApplyRedaction(
                IAsyncEnumerable<CommitRecord> source,
                RedactionPolicy policy,
                RedactionTier tier,
                [EnumeratorCancellation] CancellationToken ct)
            {
                await foreach (var record in source.WithCancellation(ct))
                {
                    yield return policy.Redact(record, tier);
                }
            }

            IAsyncEnumerable<CommitRecord> records = ApplyRedaction(
                commitParser.ParseCommitsStreamAsync(repoName, license,
                    targetDir.FullName, repoUrl, CancellationToken.None),
                redactionPolicy, redactionTier, CancellationToken.None);

            await diffJsonFileWriter.WriteToJsonFileAsync(records, outputPath, CancellationToken.None);
        }
        else
        {
            IDiffTrainingJsonFileWriter trainingWriter = serviceProvider.GetRequiredService<IDiffTrainingJsonFileWriter>();
            TrainingExampleBuilder trainingBuilder = serviceProvider.GetRequiredService<TrainingExampleBuilder>();

            PromptTemplate preset = PromptPresets.Get(promptStyle);
            string effectiveSystemTemplate = !string.IsNullOrEmpty(systemPromptOverride)
                ? systemPromptOverride
                : preset.System;
            string effectiveUserTemplate = !string.IsNullOrEmpty(userPromptOverride)
                ? userPromptOverride
                : preset.User;

            PromptTemplate effectiveTemplate = new(effectiveSystemTemplate, effectiveUserTemplate);

            ReasoningEffort parsedEffort = Enum.TryParse<ReasoningEffort>(reasoningEffortStr, ignoreCase: true, out var e) ? e : ReasoningEffort.Auto;
            ChatOptions finalOptions = chatOptionsBuilder.BuildChatOptions(parsedEffort, provider, modelId ?? "");

            TrainingExampleOptions options = new(
                effectiveTemplate,
                string.IsNullOrEmpty(llmOverridePrompt) ? null : llmOverridePrompt,
                llmAssistantOutput,
                redactionTier,
                finalOptions);

            IAsyncEnumerable<CommitRecord> rawCommits = commitParser.ParseCommitsStreamAsync(repoName, license,
                targetDir.FullName, repoUrl, CancellationToken.None);

            IAsyncEnumerable<CommitTrainingRecord> trainingRecords =
                trainingBuilder.BuildAsync(rawCommits, options, CancellationToken.None);

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


