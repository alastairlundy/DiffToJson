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
using CliInvoke.Extensions;
using DiffToJsonCli;
using DiffToJsonCli.Execution;
using DiffToJsonLib.Training.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Compliance.Redaction;

IServiceCollection services = new ServiceCollection();

// The library only depends on System.IO.Abstractions; this is where the real
// System.IO implementation is bound for every injected IFileSystem consumer.
services.AddSingleton<IFileSystem, FileSystem>();

services.AddCliInvoke();
services.AddRedaction(redaction =>
{
    redaction.SetFallbackRedactor<RegexPiiRedactor>();
});

services.AddSingleton<RedactionPolicy>(sp =>
{
    Redactor redactor = sp.GetRequiredService<Redactor>();
    return new RedactionPolicy(new Dictionary<RedactionTier, Redactor>
    {
        [RedactionTier.Message] = redactor,
        [RedactionTier.Diff] = redactor,
        [RedactionTier.All] = redactor,
    });
});
services.AddSingleton<IAssistantMessageGenerator, LlmAssistantWriter>();
services.AddSingleton<TrainingExampleBuilder>();
services.AddSingleton<IDiffJsonFileWriter, DiffJsonFileWriter>();
services.AddSingleton<IDiffTrainingJsonFileWriter, DiffTrainingJsonFileWriter>();
services.AddSingleton<IGitCommitParser, GitCommitParser>();
services.AddSingleton<CapabilityCache>();
services.AddSingleton<IReasoningEffortMatrix>(sp =>
{
    CapabilityCache cache = sp.GetRequiredService<CapabilityCache>();
    return new ModelsDevReasoningEffortMatrix(cache);
});
services.AddSingleton<IChatOptionsBuilder, ChatOptionsBuilder>();
services.AddSingleton<ILicenseAnalyzer, AILicenseAnalyzer>();
services.AddSingleton<IExtractionRunPipeline, CliExtractionRunPipeline>();

Option<DirectoryInfo> repoDirectoryOption = new("--repo-directory")
{
    Description = "The local git repository directory. Falls back to the current directory if not provided.",
    DefaultValueFactory = _ => new DirectoryInfo(Directory.GetCurrentDirectory()),
    Required = false,
};

Option<string> repoUrlOption = new("--repo-url")
{
    Description = "The URL of the git repository.",
    DefaultValueFactory = _ => "",
    Required = false,
};

Option<string> modelIdOption = new("--model-id")
{
    Description = "The model id of the AI model to use",
    DefaultValueFactory = _ => "",
    Required = false,
};

Option<string> endpointUrlOption = new("--endpoint-url")
{
    Description = "The endpoint URL of the OpenAI compatible API endpoint to use.",
    DefaultValueFactory = _ => "",
    Required = false,
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
    DefaultValueFactory = _ => "",
};

Option<string> outputFilePathOption = new("--output", ["-o"])
{
    Description = "The output file path. If not specified, the default is the repository directory path.",
    Required = false,
    DefaultValueFactory = _ => "",
};

Option<string> formatOption = new("--format")
{
    Description = "The output format. 'training' produces camelCase JSONL for AI training; 'raw' produces the legacy PascalCase JSONL.",
    DefaultValueFactory = _ => "training",
};
formatOption.AcceptOnlyFromAmong("training", "raw");

Option<string> promptStyleOption = new("--prompt-style")
{
    Description = "The prompt preset to use for training records.",
    DefaultValueFactory = _ => "default",
};
promptStyleOption.AcceptOnlyFromAmong("default", "conventional");

Option<string> systemPromptOption = new("--system-prompt")
{
    Description = "Override the system prompt template. Supports placeholders: {diff}, {commitMessage}, {repoName}, {license}, {repoUrl}.",
    DefaultValueFactory = _ => "",
};

Option<string> userPromptOption = new("--user-prompt")
{
    Description = "Override the user prompt template. Supports placeholders: {diff}, {commitMessage}, {repoName}, {license}, {repoUrl}.",
    DefaultValueFactory = _ => "",
};

Option<bool> llmAssistantOutputOption = new("--llm-assistant-output")
{
    Description = "Enable LLM-generated assistant messages for each commit. Requires --provider, --model-id, --api-key, and --endpoint-url.",
    DefaultValueFactory = _ => false,
};

Option<string> llmOverridePromptOption = new("--llm-override-prompt")
{
    Description = "Override the user prompt sent to the LLM when --llm-assistant-output is enabled. Supports placeholders.",
    DefaultValueFactory = _ => "",
};

Option<string> reasoningEffortOption = new("--reasoning-effort")
{
    Description = "The reasoning effort level used by the AI model. Valid values: auto, on, off, low, medium, high, xhigh, max. Valid values depend on the active (provider, model).",
    DefaultValueFactory = _ => "auto",
    Required = false,
};

Option<string> redactionOption = new("--redaction")
{
    Description = "PII redaction tier for training records. 'none' disables redaction; 'message' redacts only commit messages; 'diff' redacts only diffs; 'all' redacts both.",
    DefaultValueFactory = _ => "message",
};
redactionOption.AcceptOnlyFromAmong("message", "diff", "all", "none");

RootCommand rootCommand = new("Detects and Serializes Git Diffs and Commits to a .JSONL file. Exit codes: 0 on success, 1 on run failures and unexpected errors, 2 on usage errors, 130 on cancellation.")
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
    redactionOption,
};

rootCommand.SetAction(async (result, invocationToken) =>
{
    // Bind raw option values without applying fallback.
    DirectoryInfo? repoDirectoryValue = result.GetValue(repoDirectoryOption);
    string repoUrl = result.GetValue(repoUrlOption) ?? "";
    string license = result.GetValue(licenseOption) ?? "";
    string outputPath = result.GetValue(outputFilePathOption) ?? "";
    string format = result.GetValue(formatOption) ?? "training";
    string promptStyle = result.GetValue(promptStyleOption) ?? "default";
    string systemPromptOverride = result.GetValue(systemPromptOption) ?? "";
    string userPromptOverride = result.GetValue(userPromptOption) ?? "";
    bool llmAssistantOutput = result.GetValue(llmAssistantOutputOption);
    string llmOverridePrompt = result.GetValue(llmOverridePromptOption) ?? "";
    string redaction = result.GetValue(redactionOption) ?? "message";
    string reasoningEffort = result.GetValue(reasoningEffortOption) ?? "auto";
    string providerOptionValue = result.GetValue(providerOption) ?? "";
    string apiKeyOptionValue = result.GetValue(apiKeyOption) ?? "";
    string endpointOptionValue = result.GetValue(endpointUrlOption) ?? "";
    string modelOptionValue = result.GetValue(modelIdOption) ?? "";

    // Build the pipeline input with raw halves. The pipeline applies
    // option-wins-over-environment itself before validation and dispatch.
    RawValue provider = RawValue.From(providerOptionValue, Environment.GetEnvironmentVariable("DIFFTOJSON_PROVIDER") ?? "");
    RawValue credential = RawValue.From(apiKeyOptionValue, Environment.GetEnvironmentVariable("DIFFTOJSON_API_KEY") ?? "");
    RawValue endpoint = RawValue.From(endpointOptionValue, Environment.GetEnvironmentVariable("DIFFTOJSON_ENDPOINT") ?? "");
    RawValue model = RawValue.From(modelOptionValue, Environment.GetEnvironmentVariable("DIFFTOJSON_MODEL") ?? "");

    bool isRaw = string.Equals(format, "raw", StringComparison.OrdinalIgnoreCase);

    // Per-run chat credentials for the injected seams. Validation and
    // fallback authority stays in the pipeline, which re-resolves from the raw input.
    services.AddSingleton<IChatClientFactory>(_ => new ChatClientFactory(
        provider.EffectiveValue,
        credential.EffectiveValue,
        endpoint.EffectiveValue,
        model.EffectiveValue));

#pragma warning disable MA0004
    await using ServiceProvider serviceProvider = services.BuildServiceProvider();
#pragma warning restore MA0004
    IExtractionRunPipeline pipeline = serviceProvider.GetRequiredService<IExtractionRunPipeline>();

    // Ctrl+C handling is owned by System.CommandLine: invocationToken is already
    // cancelled on process termination, which the pipeline honours end to end.
    try
    {
        // Render every console line from pipeline progress, synchronously, so the
        // reported order matches the pipeline's report order. Warnings go to stderr.
        IProgress<RunProgress> progress = new ConsoleProgress();
        ExtractionRunFacts facts;
        
        if(isRaw)
        {
            RunRawInput rawInput = new(
                repoDirectoryValue?.FullName ?? "",
                repoUrl,
                license,
                outputPath,
                redaction,
                reasoningEffort,
                llmAssistantOutput,
                llmOverridePrompt,
                provider,
                credential,
                endpoint,
                model); 
            
            facts = await pipeline.RunRawAsync(rawInput, progress, invocationToken)
                .ConfigureAwait(true);
        }
        else
        {
            RunTrainingInput trainingInput = new(
                repoDirectoryValue?.FullName ?? "",
                repoUrl,
                license,
                outputPath,
                redaction,
                reasoningEffort,
                llmAssistantOutput,
                llmOverridePrompt,
                provider,
                credential,
                endpoint,
                model,
                promptStyle,
                systemPromptOverride,
                userPromptOverride);
            
            facts = await pipeline.RunTrainingAsync(trainingInput, progress, invocationToken).ConfigureAwait(true);
        }

        await Console.Out.WriteLineAsync($"Successfully wrote {facts.RecordCount} commits to {facts.OutputPath} (License: {facts.License})")
            .ConfigureAwait(true);
        return ExitCode.Success;
    }
    catch (Exception ex)
    {
        int exitCode = ExitCode.FromException(ex);
        string message = ex is OperationCanceledException ? "Cancelled." : $"Error: {ex.Message}";
        await Console.Error.WriteLineAsync(message).ConfigureAwait(true);
        return exitCode;
    }
});

ParseResult parseResult = rootCommand.Parse(args);

return await parseResult.InvokeAsync().ConfigureAwait(true);