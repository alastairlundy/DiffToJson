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

using System.Runtime.CompilerServices;
using DiffToJsonLib.Prompts;
using DiffToJsonLib.Reasoning;
using DiffToJsonLib.Training;
using DiffToJsonLib.Writers;
using Microsoft.Extensions.Compliance.Redaction;
using ReasoningEffort = DiffToJsonLib.Reasoning.ReasoningEffort;

namespace DiffToJsonCli.Execution;

/// <summary>
/// Command line-owned <see cref="IExtractionRunPipeline"/> implementation.
/// Ports the validation, option-wins-over-environment precedence, and dispatch
/// behaviors out of the command entry point with console writes and exit codes removed.
/// Progress travels only through the per-call sink; failures travel as Base Class Library exceptions.
/// Trimming-safe: no reflection, no dynamic dispatch, no per-call service location.
/// </summary>
public sealed class CliExtractionRunPipeline : IExtractionRunPipeline
{
    private readonly IGitCommitParser _commitParser;
    private readonly ILicenseAnalyzer _licenseAnalyzer;
    private readonly IReasoningEffortMatrix _effortMatrix;
    private readonly IChatOptionsBuilder _chatOptionsBuilder;
    private readonly TrainingExampleBuilder _trainingBuilder;
    private readonly IDiffJsonFileWriter _rawWriter;
    private readonly IDiffTrainingJsonFileWriter _trainingWriter;
    private readonly RedactionPolicy _redactionPolicy;

    /// <summary>
    /// Creates a pipeline from the existing abstractions. File discovery stays inside
    /// this class rather than arriving as a seam. Seven constructor parameters accepted.
    /// </summary>
    public CliExtractionRunPipeline(
        IGitCommitParser commitParser,
        ILicenseAnalyzer licenseAnalyzer,
        IReasoningEffortMatrix effortMatrix,
        IChatOptionsBuilder chatOptionsBuilder,
        TrainingExampleBuilder trainingBuilder,
        IDiffJsonFileWriter rawWriter,
        IDiffTrainingJsonFileWriter trainingWriter)
    {
        ArgumentNullException.ThrowIfNull(commitParser);
        ArgumentNullException.ThrowIfNull(licenseAnalyzer);
        ArgumentNullException.ThrowIfNull(effortMatrix);
        ArgumentNullException.ThrowIfNull(chatOptionsBuilder);
        ArgumentNullException.ThrowIfNull(trainingBuilder);
        ArgumentNullException.ThrowIfNull(rawWriter);
        ArgumentNullException.ThrowIfNull(trainingWriter);

        _commitParser = commitParser;
        _licenseAnalyzer = licenseAnalyzer;
        _effortMatrix = effortMatrix;
        _chatOptionsBuilder = chatOptionsBuilder;
        _trainingBuilder = trainingBuilder;
        _rawWriter = rawWriter;
        _trainingWriter = trainingWriter;

        // Mirrors the redaction registrations in the command setup. RegexPiiRedactor is
        // stateless, so this policy redacts identically to the service-provided one.
        Redactor redactor = new RegexPiiRedactor();
        _redactionPolicy = new RedactionPolicy(new Dictionary<RedactionTier, Redactor>
        {
            [RedactionTier.Message] = redactor,
            [RedactionTier.Diff] = redactor,
            [RedactionTier.All] = redactor,
        });
    }

    /// <inheritdoc />
    public async Task<ExtractionRunFacts> RunRawAsync(
        RunRawInput input,
        IProgress<RunProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();

        string provider = input.Provider.EffectiveValue;
        string credential = input.Credential.EffectiveValue;
        string endpoint = input.Endpoint.EffectiveValue;
        string model = input.Model.EffectiveValue;

        if (input.LlmAssistantOutput)
        {
            throw new ArgumentException(
                "--llm-assistant-output is not compatible with --format raw.", nameof(input));
        }

        if (!string.IsNullOrEmpty(input.LlmOverridePrompt) && !input.LlmAssistantOutput)
        {
            throw new ArgumentException(
                "--llm-override-prompt requires --llm-assistant-output.", nameof(input));
        }

        RedactionTier tier = ParseRedactionTier(input.Redaction);
        ReasoningEffort effort = ParseReasoningEffort(input.ReasoningEffort);

        if (effort != ReasoningEffort.Auto && !input.LlmAssistantOutput)
        {
            throw new ArgumentException(
                "--reasoning-effort requires --llm-assistant-output when set to a value other than 'auto'.",
                nameof(input));
        }

        SharedSetup setup = await ResolveSharedSetupAsync(
            input.RepoDirectory,
            input.RepoUrl,
            input.License,
            input.OutputPath,
            tier,
            effort,
            provider,
            credential,
            endpoint,
            model,
            llmAssistantOutput: false,
            progress,
            cancellationToken).ConfigureAwait(false);

        ValidatePromptOverride(input.LlmOverridePrompt, "--llm-override-prompt");

        progress?.Report(new RunProgress(RunPhase.ParsingCommits, $"Parsing commits from {setup.RepoDirectory}."));

        IAsyncEnumerable<CommitRecord> commits = ApplyRedaction(
            _commitParser.ParseCommitsStreamAsync(
                setup.RepoName, setup.License, setup.RepoDirectory, setup.RepoUrl, cancellationToken),
            _redactionPolicy,
            setup.Tier,
            cancellationToken);

        int recordCount = 0;
        IAsyncEnumerable<CommitRecord> counted = CountRecords(commits, () => recordCount++, cancellationToken);

        progress?.Report(new RunProgress(RunPhase.WritingOutput, $"Writing output to {setup.OutputPath}."));
        await _rawWriter.WriteToJsonFileAsync(counted, setup.OutputPath, cancellationToken).ConfigureAwait(false);

        return new ExtractionRunFacts(setup.OutputPath, setup.License, recordCount);
    }

    /// <inheritdoc />
    public async Task<ExtractionRunFacts> RunTrainingAsync(
        RunTrainingInput input,
        IProgress<RunProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();

        string provider = input.Provider.EffectiveValue;
        string credential = input.Credential.EffectiveValue;
        string endpoint = input.Endpoint.EffectiveValue;
        string model = input.Model.EffectiveValue;

        if (!string.IsNullOrEmpty(input.LlmOverridePrompt) && !input.LlmAssistantOutput)
        {
            throw new ArgumentException(
                "--llm-override-prompt requires --llm-assistant-output.", nameof(input));
        }

        RedactionTier tier = ParseRedactionTier(input.Redaction);
        ReasoningEffort effort = ParseReasoningEffort(input.ReasoningEffort);

        if (effort != ReasoningEffort.Auto && !input.LlmAssistantOutput)
        {
            throw new ArgumentException(
                "--reasoning-effort requires --llm-assistant-output when set to a value other than 'auto'.",
                nameof(input));
        }

        SharedSetup setup = await ResolveSharedSetupAsync(
            input.RepoDirectory,
            input.RepoUrl,
            input.License,
            input.OutputPath,
            tier,
            effort,
            provider,
            credential,
            endpoint,
            model,
            input.LlmAssistantOutput,
            progress,
            cancellationToken).ConfigureAwait(false);

        // The capability cache is only touched on LLM runs: auto is accepted by every
        // model, so only a non-auto effort needs a support query. Single query, parsed once.
        if (input.LlmAssistantOutput && effort != ReasoningEffort.Auto)
        {
            IReadOnlySet<ReasoningEffort> supported = _effortMatrix.GetSupportedReasoningValues(model);
            if (!supported.Contains(effort))
            {
                string validList = string.Join(", ", supported.Select(v => v.ToString().ToLowerInvariant()));
                throw new NotSupportedException(
                    $"--reasoning-effort '{input.ReasoningEffort}' is not supported for provider '{provider}', model '{model}'. Supported values: {validList}");
            }
        }

        string promptStyle = string.IsNullOrEmpty(input.PromptStyle) ? "default" : input.PromptStyle;
        PromptTemplate preset = PromptPresets.Get(promptStyle);

        string effectiveSystem = !string.IsNullOrEmpty(input.SystemPromptOverride)
            ? input.SystemPromptOverride
            : preset.System;
        string effectiveUser = !string.IsNullOrEmpty(input.UserPromptOverride)
            ? input.UserPromptOverride
            : preset.User;
        PromptTemplate effectiveTemplate = new(effectiveSystem, effectiveUser);

        ValidateTemplate(effectiveTemplate);
        ValidatePromptOverride(input.LlmOverridePrompt, "--llm-override-prompt");

        // Chat options (and the matrix queries behind the builder) stay behind LLM runs
        // so non-LLM runs never pay for capability-cache I/O. The options are unused
        // when assistant output is disabled.
        ChatOptions chatOptions = input.LlmAssistantOutput
            ? _chatOptionsBuilder.BuildChatOptions(effort, provider, model)
            : new ChatOptions();

        TrainingExampleBuilder activeBuilder = _trainingBuilder;
        if (input.LlmAssistantOutput)
        {
            // Setup detail, not a seam: per-call credentials flow into a fresh factory.
            // Credentials were validated above, before construction.
            ChatClientFactory factory = new(provider, credential, endpoint, model);
            activeBuilder = new TrainingExampleBuilder(
                _redactionPolicy,
                new LlmAssistantWriter(factory, _redactionPolicy));
        }

        TrainingExampleOptions options = new(
            effectiveTemplate,
            string.IsNullOrEmpty(input.LlmOverridePrompt) ? null : input.LlmOverridePrompt,
            input.LlmAssistantOutput,
            setup.Tier,
            chatOptions);

        progress?.Report(new RunProgress(RunPhase.ParsingCommits, $"Parsing commits from {setup.RepoDirectory}."));

        IAsyncEnumerable<CommitRecord> rawCommits = _commitParser.ParseCommitsStreamAsync(
            setup.RepoName, setup.License, setup.RepoDirectory, setup.RepoUrl, cancellationToken);

        progress?.Report(new RunProgress(RunPhase.BuildingExamples, "Building training examples."));

        IAsyncEnumerable<CommitTrainingRecord> trainingRecords =
            activeBuilder.BuildAsync(rawCommits, options, cancellationToken);

        int recordCount = 0;
        IAsyncEnumerable<CommitTrainingRecord> counted =
            CountRecords(trainingRecords, () => recordCount++, cancellationToken);

        progress?.Report(new RunProgress(RunPhase.WritingOutput, $"Writing output to {setup.OutputPath}."));
        await _trainingWriter.WriteToJsonFileAsync(counted, setup.OutputPath, cancellationToken).ConfigureAwait(false);

        return new ExtractionRunFacts(setup.OutputPath, setup.License, recordCount);
    }

    /// <summary>
    /// Shared setup built once per call: directories, output path, credential gates,
    /// and Legal Metadata with Unknown fallback. Reused by both entry points.
    /// </summary>
    private async Task<SharedSetup> ResolveSharedSetupAsync(
        string repoDirectory,
        string repoUrl,
        string licenseProvided,
        string outputPathRaw,
        RedactionTier tier,
        ReasoningEffort effort,
        string provider,
        string credential,
        string endpoint,
        string model,
        bool llmAssistantOutput,
        IProgress<RunProgress>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string workingDir = string.IsNullOrEmpty(repoDirectory)
            ? Directory.GetCurrentDirectory()
            : new DirectoryInfo(repoDirectory).FullName;
        string repoName = new DirectoryInfo(workingDir).Name;

        string outputPath;
        if (string.IsNullOrEmpty(outputPathRaw))
        {
            outputPath = Path.Combine(workingDir, $"{repoName}-commits.jsonl");
        }
        else if (outputPathRaw.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase))
        {
            outputPath = Path.GetFullPath(outputPathRaw);
        }
        else
        {
            outputPath = Path.Combine(Path.GetFullPath(outputPathRaw), $"{repoName}-commits.jsonl");
        }

        if (string.IsNullOrEmpty(licenseProvided))
        {
            ArgumentException.ThrowIfNullOrEmpty(endpoint);
            ArgumentException.ThrowIfNullOrEmpty(model);
        }

        if (llmAssistantOutput)
        {
            ArgumentException.ThrowIfNullOrEmpty(model);
        }

        cancellationToken.ThrowIfCancellationRequested();

        string license;
        if (!string.IsNullOrEmpty(licenseProvided))
        {
            license = licenseProvided;
            progress?.Report(new RunProgress(RunPhase.ResolvingLicense, $"Using specified license: {license}."));
        }
        else
        {
            progress?.Report(new RunProgress(RunPhase.ResolvingLicense, "Resolving license from repository directory."));
            FileInfo? licenseFile = FindLicenseFile(workingDir);
            cancellationToken.ThrowIfCancellationRequested();

            if (licenseFile is not null)
            {
                license = await _licenseAnalyzer.AnalyzeLicenseAsync(licenseFile).ConfigureAwait(false) ?? "Unknown";
            }
            else
            {
                license = "Unknown";
            }

            progress?.Report(new RunProgress(RunPhase.ResolvingLicense, $"Detected license: {license}."));
        }

        return new SharedSetup(
            workingDir, repoName, repoUrl ?? string.Empty, license, outputPath,
            tier, effort, provider, credential, endpoint, model);
    }

    private sealed record SharedSetup(
        string RepoDirectory,
        string RepoName,
        string RepoUrl,
        string License,
        string OutputPath,
        RedactionTier Tier,
        ReasoningEffort Effort,
        string Provider,
        string Credential,
        string Endpoint,
        string Model);

    private static RedactionTier ParseRedactionTier(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return RedactionTier.Message;
        }

        if (Enum.TryParse<RedactionTier>(value, ignoreCase: true, out RedactionTier tier) &&
            Enum.IsDefined(typeof(RedactionTier), tier))
        {
            return tier;
        }

        throw new ArgumentException(
            $"--redaction '{value}' is not a valid value. Supported values: none, message, diff, all.",
            nameof(value));
    }

    private static ReasoningEffort ParseReasoningEffort(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return ReasoningEffort.Auto;
        }

        if (Enum.TryParse<ReasoningEffort>(value, ignoreCase: true, out ReasoningEffort effort) &&
            Enum.IsDefined(typeof(ReasoningEffort), effort))
        {
            return effort;
        }

        throw new ArgumentException(
            $"--reasoning-effort '{value}' is not a valid value. Supported values: auto, on, off, low, medium, high, xhigh, max.",
            nameof(value));
    }

    private static void ValidateTemplate(PromptTemplate template)
    {
        try
        {
            PromptPresets.ValidateTemplate(template);
        }
        catch (PromptTemplateValidationException ex)
        {
            throw new ArgumentException(ex.Message, nameof(template), ex);
        }
    }

    private static void ValidatePromptOverride(string overridePrompt, string flagName)
    {
        if (string.IsNullOrEmpty(overridePrompt))
        {
            return;
        }

        try
        {
            PromptPresets.ValidateTemplate(new PromptTemplate(overridePrompt, string.Empty));
        }
        catch (PromptTemplateValidationException ex)
        {
            throw new ArgumentException($"Error in {flagName}: {ex.Message}", flagName, ex);
        }
    }

    /// <summary>
    /// File discovery traveling with this implementation, not a seam.
    /// Mirrors the license-file priority order.
    /// </summary>
    private static FileInfo? FindLicenseFile(string workingDir)
    {
        string[] priorityFiles = ["LICENSE.md", "LICENSE.txt", "LICENSE"];

        foreach (string fileName in priorityFiles)
        {
            string path = Path.Combine(workingDir, fileName);
            if (File.Exists(path))
            {
                return new FileInfo(path);
            }
        }

        return null;
    }

    private static async IAsyncEnumerable<CommitRecord> ApplyRedaction(
        IAsyncEnumerable<CommitRecord> source,
        RedactionPolicy policy,
        RedactionTier tier,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (CommitRecord record in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return policy.Redact(record, tier);
        }
    }

    private static async IAsyncEnumerable<T> CountRecords<T>(
        IAsyncEnumerable<T> source,
        Action onRecord,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (T record in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            onRecord();
            yield return record;
        }
    }
}
