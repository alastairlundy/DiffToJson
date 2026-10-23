using DiffToJsonLib.Training;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Compliance.Redaction;
using ModelsDotDevSharp;
using ReasoningEffort = DiffToJsonLib.Reasoning.ReasoningEffort;

namespace DiffToJsonCli.Tests;

/// <summary>
/// Covers the CLI-owned <see cref="CliExtractionRunPipeline"/> implementation:
/// precedence, cross-option gates, Unknown fallback, effort errors,
/// both dispatch paths, sink phases, facts content, and cancellation.
/// </summary>
public sealed class CliExtractionRunPipelineTests
{
    #region Fakes

    private sealed class FakeCommitParser : IGitCommitParser
    {
        public List<CommitRecord> Commits { get; } = new();
        public Exception? Throw { get; set; }
        public string? CapturedRepoName { get; private set; }
        public string? CapturedLicense { get; private set; }
        public string? CapturedWorkingDir { get; private set; }
        public string? CapturedRepoUrl { get; private set; }

        public Task<CommitRecord[]> ParseCommitsToArrayAsync(
            string repoName, string license, string workingDir, string repoUrl, CancellationToken cancellationToken)
        {
            if (Throw is not null)
            {
                throw Throw;
            }

            cancellationToken.ThrowIfCancellationRequested();
            CapturedRepoName = repoName;
            CapturedLicense = license;
            CapturedWorkingDir = workingDir;
            CapturedRepoUrl = repoUrl;
            return Task.FromResult(Commits.ToArray());
        }

        public async IAsyncEnumerable<CommitRecord> ParseCommitsStreamAsync(
            string repoName, string license, string workingDir, string repoUrl,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (Throw is not null)
            {
                throw Throw;
            }

            cancellationToken.ThrowIfCancellationRequested();
            CapturedRepoName = repoName;
            CapturedLicense = license;
            CapturedWorkingDir = workingDir;
            CapturedRepoUrl = repoUrl;

            foreach (CommitRecord commit in Commits)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return commit;
                await Task.CompletedTask;
            }
        }
    }

    private sealed class FakeLicenseAnalyzer : ILicenseAnalyzer
    {
        public string? Result { get; set; } = "MIT";
        public int CallCount { get; private set; }
        public IFileInfo? CapturedFile { get; private set; }

        public Task<string?> AnalyzeLicenseAsync(IFileInfo licenseFile)
        {
            CallCount++;
            CapturedFile = licenseFile;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeEffortMatrix : IReasoningEffortMatrix
    {
        public IReadOnlySet<ReasoningEffort> Supported { get; set; } =
            new HashSet<ReasoningEffort> { ReasoningEffort.Auto };
        public int CallCount { get; private set; }
        public CacheStatus Status { get; set; } = CacheStatus.Fresh;
        public int CacheStatusCallCount { get; private set; }
        public string? CapturedModel { get; private set; }

        public IReadOnlySet<ReasoningEffort> GetSupportedReasoningValues(string model)
        {
            CallCount++;
            CapturedModel = model;
            return Supported;
        }

        public bool ProducesReasoningOnAuto(string model) => false;

        public AIModelReasoningOptionType? GetReasoningType(string model, string provider) => null;

        public Task<CacheStatus> GetCacheStatusAsync(CancellationToken cancellationToken = default)
        {
            CacheStatusCallCount++;
            return Task.FromResult(Status);
        }
    }

    private sealed class FakeChatOptionsBuilder : IChatOptionsBuilder
    {
        public int CallCount { get; private set; }
        public ReasoningEffort CapturedEffort { get; private set; }
        public string? CapturedProvider { get; private set; }
        public string? CapturedModel { get; private set; }

        public ChatOptions BuildChatOptions(ReasoningEffort reasoningEffort, string provider, string model)
        {
            CallCount++;
            CapturedEffort = reasoningEffort;
            CapturedProvider = provider;
            CapturedModel = model;
            return new ChatOptions();
        }
    }

    private sealed class FakeRawWriter : IDiffJsonFileWriter
    {
        public List<CommitRecord> Written { get; } = new();
        public string? CapturedPath { get; private set; }
        public int CallCount { get; private set; }
        public Exception? Throw { get; set; }

        public async Task WriteToJsonFileAsync(
            IAsyncEnumerable<CommitRecord> commits, string filePath, CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (Throw is not null)
            {
                throw Throw;
            }

            CapturedPath = filePath;
            Written.Clear();
            await foreach (CommitRecord commit in commits.WithCancellation(cancellationToken))
            {
                Written.Add(commit);
            }
        }

        public Task WriteToJsonFileAsync(
            ICollection<CommitRecord> commits, string filePath, CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (Throw is not null)
            {
                throw Throw;
            }

            CapturedPath = filePath;
            Written.Clear();
            Written.AddRange(commits);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTrainingWriter : IDiffTrainingJsonFileWriter
    {
        public List<CommitTrainingRecord> Written { get; } = new();
        public string? CapturedPath { get; private set; }
        public int CallCount { get; private set; }
        public Exception? Throw { get; set; }

        public async Task WriteToJsonFileAsync(
            IAsyncEnumerable<CommitTrainingRecord> records, string filePath, CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (Throw is not null)
            {
                throw Throw;
            }

            CapturedPath = filePath;
            Written.Clear();
            await foreach (CommitTrainingRecord record in records.WithCancellation(cancellationToken))
            {
                Written.Add(record);
            }
        }
    }

    /// <summary>
    /// Synchronous progress sink driven by a lambda, capturing <see cref="RunProgress"/> values.
    /// </summary>
    private sealed class LambdaProgress(Action<RunProgress> onReport) : IProgress<RunProgress>
    {
        public void Report(RunProgress value) => onReport(value);
    }

    #endregion

    #region Helpers

    private sealed record TestSeams(
        FakeCommitParser Parser,
        FakeLicenseAnalyzer License,
        FakeEffortMatrix Matrix,
        FakeChatOptionsBuilder ChatOptions,
        FakeRawWriter RawWriter,
        FakeTrainingWriter TrainingWriter);

    private static TestSeams CreateSeams()
    {
        return new TestSeams(
            new FakeCommitParser(),
            new FakeLicenseAnalyzer(),
            new FakeEffortMatrix(),
            new FakeChatOptionsBuilder(),
            new FakeRawWriter(),
            new FakeTrainingWriter());
    }

    private static CliExtractionRunPipeline CreatePipeline(TestSeams seams)
    {
        RedactionPolicy policy = new(new Dictionary<RedactionTier, Redactor>());
        return new CliExtractionRunPipeline(
            new FileSystem(),
            seams.Parser,
            seams.License,
            seams.Matrix,
            seams.ChatOptions,
            new TrainingExampleBuilder(policy, new DisabledAssistantMessageGenerator()),
            seams.RawWriter,
            seams.TrainingWriter,
            policy);
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTempDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup only.
        }
    }

    private static CommitRecord SampleCommit(string suffix = "1") => new(
        Diff: $"diff content {suffix}",
        CommitMessage: $"fix bug {suffix}",
        RepoName: "test/repo",
        License: "MIT",
        RepoUrl: "https://github.com/test/repo");

    private static RunRawInput ValidRawInput(
        string repoDirectory,
        string license = "MIT",
        string outputPath = "",
        string redaction = "message",
        string reasoningEffort = "auto",
        bool llmAssistantOutput = false,
        string llmOverridePrompt = "",
        RawValue? provider = null,
        RawValue? credential = null,
        RawValue? endpoint = null,
        RawValue? model = null) => new(
        repoDirectory,
        "https://github.com/test/repo",
        license,
        outputPath,
        redaction,
        reasoningEffort,
        llmAssistantOutput,
        llmOverridePrompt,
        provider ?? RawValue.From("", ""),
        credential ?? RawValue.From("", ""),
        endpoint ?? RawValue.From("", ""),
        model ?? RawValue.From("", ""));

    private static RunTrainingInput ValidTrainingInput(
        string repoDirectory,
        string license = "MIT",
        string outputPath = "",
        string redaction = "message",
        string reasoningEffort = "auto",
        bool llmAssistantOutput = false,
        string llmOverridePrompt = "",
        RawValue? provider = null,
        RawValue? credential = null,
        RawValue? endpoint = null,
        RawValue? model = null,
        string promptStyle = "default",
        string systemOverride = "",
        string userOverride = "") => new(
        repoDirectory,
        "https://github.com/test/repo",
        license,
        outputPath,
        redaction,
        reasoningEffort,
        llmAssistantOutput,
        llmOverridePrompt,
        provider ?? RawValue.From("", ""),
        credential ?? RawValue.From("", ""),
        endpoint ?? RawValue.From("", ""),
        model ?? RawValue.From("", ""),
        promptStyle,
        systemOverride,
        userOverride);

    #endregion

    #region Precedence matrix for the four RawValue fields

    [Test]
    public async Task RawValue_OptionWinsOverEnvironment_ForAllFourFields()
    {
        RunRawInput raw = ValidRawInput(
            repoDirectory: "/tmp/repo",
            provider: RawValue.From("opt-provider", "env-provider"),
            credential: RawValue.From("opt-key", "env-key"),
            endpoint: RawValue.From("opt-endpoint", "env-endpoint"),
            model: RawValue.From("opt-model", "env-model"));

        await Assert.That(raw.Provider.EffectiveValue).IsEqualTo("opt-provider");
        await Assert.That(raw.Credential.EffectiveValue).IsEqualTo("opt-key");
        await Assert.That(raw.Endpoint.EffectiveValue).IsEqualTo("opt-endpoint");
        await Assert.That(raw.Model.EffectiveValue).IsEqualTo("opt-model");

        RunTrainingInput training = ValidTrainingInput(
            repoDirectory: "/tmp/repo",
            provider: RawValue.From("opt-provider", "env-provider"),
            credential: RawValue.From("opt-key", "env-key"),
            endpoint: RawValue.From("opt-endpoint", "env-endpoint"),
            model: RawValue.From("opt-model", "env-model"));

        await Assert.That(training.Provider.EffectiveValue).IsEqualTo("opt-provider");
        await Assert.That(training.Credential.EffectiveValue).IsEqualTo("opt-key");
        await Assert.That(training.Endpoint.EffectiveValue).IsEqualTo("opt-endpoint");
        await Assert.That(training.Model.EffectiveValue).IsEqualTo("opt-model");
    }

    [Test]
    public async Task RawValue_EnvironmentUsedWhenOptionEmpty_ForAllFourFields()
    {
        RawValue provider = RawValue.From("", "env-provider");
        RawValue credential = RawValue.From("", "env-key");
        RawValue endpoint = RawValue.From("", "env-endpoint");
        RawValue model = RawValue.From("", "env-model");

        await Assert.That(provider.EffectiveValue).IsEqualTo("env-provider");
        await Assert.That(credential.EffectiveValue).IsEqualTo("env-key");
        await Assert.That(endpoint.EffectiveValue).IsEqualTo("env-endpoint");
        await Assert.That(model.EffectiveValue).IsEqualTo("env-model");
    }

    [Test]
    public async Task RawValue_BothEmpty_MeansUnset()
    {
        RawValue value = RawValue.From("", "");

        await Assert.That(value.EffectiveValue).IsEqualTo(string.Empty);
        await Assert.That(RawValue.Resolve("", "")).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task RawValue_Resolve_MatchesEffectiveValue()
    {
        await Assert.That(RawValue.Resolve("opt", "env")).IsEqualTo("opt");
        await Assert.That(RawValue.Resolve("", "env")).IsEqualTo("env");
        await Assert.That(RawValue.Resolve("opt", "")).IsEqualTo("opt");
    }

    [Test]
    public async Task Pipeline_ModelAndProviderPrecedence_OptionWinsObservedViaNotSupported()
    {
        TestSeams seams = CreateSeams();
        seams.Matrix.Supported = new HashSet<ReasoningEffort> { ReasoningEffort.Auto };
        CliExtractionRunPipeline pipeline = CreatePipeline(seams);

        string tempDir = CreateTempDirectory();
        try
        {
            RunTrainingInput input = ValidTrainingInput(
                tempDir,
                license: "MIT",
                reasoningEffort: "high",
                llmAssistantOutput: true,
                provider: RawValue.From("opt-provider", "env-provider"),
                credential: RawValue.From("opt-key", "env-key"),
                endpoint: RawValue.From("https://example.com", "https://env.example.com"),
                model: RawValue.From("opt-model", "env-model"));

            NotSupportedException? caught = null;
            try
            {
                await pipeline.RunTrainingAsync(
                    input, new LambdaProgress(_ => { }), CancellationToken.None);
            }
            catch (NotSupportedException ex)
            {
                caught = ex;
            }

            await Assert.That(caught).IsNotNull();
            await Assert.That(caught.Message.Contains("opt-provider")).IsTrue();
            await Assert.That(caught.Message.Contains("opt-model")).IsTrue();
            await Assert.That(caught.Message.Contains("env-provider")).IsFalse();
            await Assert.That(caught.Message.Contains("env-model")).IsFalse();
            await Assert.That(seams.Matrix.CapturedModel).IsEqualTo("opt-model");
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }

    [Test]
    public async Task Pipeline_EndpointModel_EnvFallback_AllowsLicenseFallback()
    {
        TestSeams seams = CreateSeams();
        seams.Parser.Commits.Add(SampleCommit());
        CliExtractionRunPipeline pipeline = CreatePipeline(seams);

        string tempDir = CreateTempDirectory();
        try
        {
            // License missing, so endpoint plus model are required. Option halves are
            // empty, proving the environment halves satisfy the gate.
            RunTrainingInput input = ValidTrainingInput(
                tempDir,
                license: "",
                endpoint: RawValue.From("", "https://env.example.com"),
                model: RawValue.From("", "env-model"));

            ExtractionRunFacts facts = await pipeline.RunTrainingAsync(
                input, progress: null, CancellationToken.None);

            await Assert.That(facts.License).IsEqualTo("Unknown");
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }

    [Test]
    public async Task Pipeline_EndpointModel_BothEmpty_ThrowsArgumentException_WhenLicenseMissing()
    {
        TestSeams seams = CreateSeams();
        CliExtractionRunPipeline pipeline = CreatePipeline(seams);

        string tempDir = CreateTempDirectory();
        try
        {
            RunTrainingInput input = ValidTrainingInput(
                tempDir,
                license: "",
                endpoint: RawValue.From("", ""),
                model: RawValue.From("", ""));

            ArgumentException? caught = null;
            try
            {
                await pipeline.RunTrainingAsync(input, progress: null, CancellationToken.None);
            }
            catch (ArgumentException ex)
            {
                caught = ex;
            }

            await Assert.That(caught).IsNotNull();
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }

    #endregion

    #region Cross-option gates

    [Test]
    public async Task Raw_LlmFlag_IncompatibleWithRawFormat()
    {
        TestSeams seams = CreateSeams();
        CliExtractionRunPipeline pipeline = CreatePipeline(seams);

        string tempDir = CreateTempDirectory();
        try
        {
            RunRawInput input = ValidRawInput(tempDir, llmAssistantOutput: true);

            ArgumentException? caught = null;
            try
            {
                await pipeline.RunRawAsync(input, progress: null, CancellationToken.None);
            }
            catch (ArgumentException ex)
            {
                caught = ex;
            }

            await Assert.That(caught).IsNotNull();
            await Assert.That(caught.Message.Contains("--llm-assistant-output")).IsTrue();
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }

    [Test]
    public async Task Raw_OverridePrompt_WithoutFlag_ThrowsArgumentException()
    {
        TestSeams seams = CreateSeams();
        CliExtractionRunPipeline pipeline = CreatePipeline(seams);

        string tempDir = CreateTempDirectory();
        try
        {
            RunRawInput input = ValidRawInput(
                tempDir, llmAssistantOutput: false, llmOverridePrompt: "custom {diff}");

            ArgumentException? caught = null;
            try
            {
                await pipeline.RunRawAsync(input, progress: null, CancellationToken.None);
            }
            catch (ArgumentException ex)
            {
                caught = ex;
            }

            await Assert.That(caught).IsNotNull();
            await Assert.That(caught.Message.Contains("--llm-override-prompt")).IsTrue();
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }

    [Test]
    public async Task Training_OverridePrompt_WithoutFlag_ThrowsArgumentException()
    {
        TestSeams seams = CreateSeams();
        CliExtractionRunPipeline pipeline = CreatePipeline(seams);

        string tempDir = CreateTempDirectory();
        try
        {
            RunTrainingInput input = ValidTrainingInput(
                tempDir, llmAssistantOutput: false, llmOverridePrompt: "custom {diff}");

            ArgumentException? caught = null;
            try
            {
                await pipeline.RunTrainingAsync(input, progress: null, CancellationToken.None);
            }
            catch (ArgumentException ex)
            {
                caught = ex;
            }

            await Assert.That(caught).IsNotNull();
            await Assert.That(caught.Message.Contains("--llm-override-prompt")).IsTrue();
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }

    [Test]
    public async Task Raw_ReasoningEffort_WithoutFlag_ThrowsArgumentException()
    {
        TestSeams seams = CreateSeams();
        CliExtractionRunPipeline pipeline = CreatePipeline(seams);

        string tempDir = CreateTempDirectory();
        try
        {
            RunRawInput input = ValidRawInput(
                tempDir, reasoningEffort: "high", llmAssistantOutput: false);

            ArgumentException? caught = null;
            try
            {
                await pipeline.RunRawAsync(input, progress: null, CancellationToken.None);
            }
            catch (ArgumentException ex)
            {
                caught = ex;
            }

            await Assert.That(caught).IsNotNull();
            await Assert.That(caught.Message.Contains("--reasoning-effort")).IsTrue();
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }

    [Test]
    public async Task Training_ReasoningEffort_WithoutFlag_ThrowsArgumentException()
    {
        TestSeams seams = CreateSeams();
        CliExtractionRunPipeline pipeline = CreatePipeline(seams);

        string tempDir = CreateTempDirectory();
        try
        {
            RunTrainingInput input = ValidTrainingInput(
                tempDir, reasoningEffort: "high", llmAssistantOutput: false);

            ArgumentException? caught = null;
            try
            {
                await pipeline.RunTrainingAsync(input, progress: null, CancellationToken.None);
            }
            catch (ArgumentException ex)
            {
                caught = ex;
            }

            await Assert.That(caught).IsNotNull();
            await Assert.That(caught.Message.Contains("--reasoning-effort")).IsTrue();
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }

    #endregion

    #region Unknown license fallback

    [Test]
    public async Task Raw_UnknownFallback_WhenNoLicenseFile()
    {
        TestSeams seams = CreateSeams();
        seams.Parser.Commits.Add(SampleCommit());
        CliExtractionRunPipeline pipeline = CreatePipeline(seams);

        string tempDir = CreateTempDirectory();
        try
        {
            RunRawInput input = ValidRawInput(
                tempDir,
                license: "",
                endpoint: RawValue.From("https://example.com", ""),
                model: RawValue.From("test-model", ""));

            ExtractionRunFacts facts = await pipeline.RunRawAsync(
                input, progress: null, CancellationToken.None);

            await Assert.That(facts.License).IsEqualTo("Unknown");
            await Assert.That(seams.License.CallCount).IsEqualTo(0);
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }

    [Test]
    public async Task Training_UnknownFallback_WhenNoLicenseFile()
    {
        TestSeams seams = CreateSeams();
        seams.Parser.Commits.Add(SampleCommit());
        CliExtractionRunPipeline pipeline = CreatePipeline(seams);

        string tempDir = CreateTempDirectory();
        try
        {
            RunTrainingInput input = ValidTrainingInput(
                tempDir,
                license: "",
                endpoint: RawValue.From("https://example.com", ""),
                model: RawValue.From("test-model", ""));

            ExtractionRunFacts facts = await pipeline.RunTrainingAsync(
                input, progress: null, CancellationToken.None);

            await Assert.That(facts.License).IsEqualTo("Unknown");
            await Assert.That(seams.License.CallCount).IsEqualTo(0);
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }

    [Test]
    public async Task UnknownFallback_WhenAnalyzerReturnsNull()
    {
        TestSeams seams = CreateSeams();
        seams.Parser.Commits.Add(SampleCommit());
        seams.License.Result = null;
        CliExtractionRunPipeline pipeline = CreatePipeline(seams);

        string tempDir = CreateTempDirectory();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(tempDir, "LICENSE.txt"), "some license text");

            RunTrainingInput input = ValidTrainingInput(
                tempDir,
                license: "",
                endpoint: RawValue.From("https://example.com", ""),
                model: RawValue.From("test-model", ""));

            ExtractionRunFacts facts = await pipeline.RunTrainingAsync(
                input, progress: null, CancellationToken.None);

            await Assert.That(facts.License).IsEqualTo("Unknown");
            await Assert.That(seams.License.CallCount).IsEqualTo(1);
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }

    [Test]
    public async Task ProvidedLicense_UsedDirectly_WithoutAnalyzer()
    {
        TestSeams seams = CreateSeams();
        seams.Parser.Commits.Add(SampleCommit());
        CliExtractionRunPipeline pipeline = CreatePipeline(seams);

        string tempDir = CreateTempDirectory();
        try
        {
            RunTrainingInput input = ValidTrainingInput(tempDir, license: "Apache-2.0");

            ExtractionRunFacts facts = await pipeline.RunTrainingAsync(
                input, progress: null, CancellationToken.None);

            await Assert.That(facts.License).IsEqualTo("Apache-2.0");
            await Assert.That(seams.License.CallCount).IsEqualTo(0);
            await Assert.That(seams.Parser.CapturedLicense).IsEqualTo("Apache-2.0");
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }

    [Test]
    public async Task DetectedLicense_UsedWhenFileFound()
    {
        TestSeams seams = CreateSeams();
        seams.Parser.Commits.Add(SampleCommit());
        seams.License.Result = "Apache-2.0";
        CliExtractionRunPipeline pipeline = CreatePipeline(seams);

        string tempDir = CreateTempDirectory();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(tempDir, "LICENSE.txt"), "Apache License text");

            RunTrainingInput input = ValidTrainingInput(
                tempDir,
                license: "",
                endpoint: RawValue.From("https://example.com", ""),
                model: RawValue.From("test-model", ""));

            ExtractionRunFacts facts = await pipeline.RunTrainingAsync(
                input, progress: null, CancellationToken.None);

            await Assert.That(facts.License).IsEqualTo("Apache-2.0");
            await Assert.That(seams.License.CallCount).IsEqualTo(1);
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }

    #endregion

    #region Effort validation errors

    [Test]
    public async Task InvalidEffortString_ThrowsNotSupportedException_WithSupportedList()
    {
        TestSeams seams = CreateSeams();
        CliExtractionRunPipeline pipeline = CreatePipeline(seams);

        string tempDir = CreateTempDirectory();
        try
        {
            RunTrainingInput input = ValidTrainingInput(
                tempDir, reasoningEffort: "ultra");

            NotSupportedException? caught = null;
            try
            {
                await pipeline.RunTrainingAsync(input, progress: null, CancellationToken.None);
            }
            catch (NotSupportedException ex)
            {
                caught = ex;
            }

            await Assert.That(caught).IsNotNull();
            await Assert.That(caught.Message.Contains("Supported values:")).IsTrue();
            await Assert.That(caught.Message.Contains("auto")).IsTrue();
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }

    [Test]
    public async Task Raw_InvalidEffortString_ThrowsNotSupportedException()
    {
        TestSeams seams = CreateSeams();
        CliExtractionRunPipeline pipeline = CreatePipeline(seams);

        string tempDir = CreateTempDirectory();
        try
        {
            RunRawInput input = ValidRawInput(tempDir, reasoningEffort: "ultra");

            NotSupportedException? caught = null;
            try
            {
                await pipeline.RunRawAsync(input, progress: null, CancellationToken.None);
            }
            catch (NotSupportedException ex)
            {
                caught = ex;
            }

            await Assert.That(caught).IsNotNull();
            await Assert.That(caught.Message.Contains("Supported values:")).IsTrue();
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }

    [Test]
    public async Task UnsupportedEffort_ThrowsNotSupported_WithSupportedList()
    {
        TestSeams seams = CreateSeams();
        seams.Matrix.Supported = new HashSet<ReasoningEffort>
        {
            ReasoningEffort.Auto, ReasoningEffort.Off
        };
        CliExtractionRunPipeline pipeline = CreatePipeline(seams);

        string tempDir = CreateTempDirectory();
        try
        {
            RunTrainingInput input = ValidTrainingInput(
                tempDir,
                license: "MIT",
                reasoningEffort: "high",
                llmAssistantOutput: true,
                provider: RawValue.From("test-provider", ""),
                model: RawValue.From("test-model", ""));

            NotSupportedException? caught = null;
            try
            {
                await pipeline.RunTrainingAsync(input, progress: null, CancellationToken.None);
            }
            catch (NotSupportedException ex)
            {
                caught = ex;
            }

            await Assert.That(caught).IsNotNull();
            await Assert.That(caught.Message.Contains("test-provider")).IsTrue();
            await Assert.That(caught.Message.Contains("test-model")).IsTrue();
            await Assert.That(caught.Message.Contains("Supported values:")).IsTrue();
            await Assert.That(caught.Message.Contains("auto")).IsTrue();
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }

    [Test]
    public async Task InvalidRedactionString_ThrowsArgumentException()
    {
        TestSeams seams = CreateSeams();
        CliExtractionRunPipeline pipeline = CreatePipeline(seams);

        string tempDir = CreateTempDirectory();
        try
        {
            RunTrainingInput input = ValidTrainingInput(tempDir, redaction: "bogus");

            ArgumentException? caught = null;
            try
            {
                await pipeline.RunTrainingAsync(input, progress: null, CancellationToken.None);
            }
            catch (ArgumentException ex)
            {
                caught = ex;
            }

            await Assert.That(caught).IsNotNull();
            await Assert.That(caught.Message.Contains("Supported values:")).IsTrue();
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }

    #endregion

    #region Both dispatch paths, sink phases, facts content

    [Test]
    public async Task Raw_Dispatch_NeverReportsBuildingExamples()
    {
        TestSeams seams = CreateSeams();
        // ReSharper disable once RedundantArgumentDefaultValue
        seams.Parser.Commits.Add(SampleCommit("1"));
        seams.Parser.Commits.Add(SampleCommit("2"));
        CliExtractionRunPipeline pipeline = CreatePipeline(seams);

        string tempDir = CreateTempDirectory();
        try
        {
            RunRawInput input = ValidRawInput(tempDir);

            List<RunProgress> seen = new();
            ExtractionRunFacts facts = await pipeline.RunRawAsync(
                input, new LambdaProgress(p => seen.Add(p)), CancellationToken.None);

            await Assert.That(seen.Exists(p => p.Phase == RunPhase.BuildingExamples)).IsFalse();
            await Assert.That(seen.Exists(p => p.Phase == RunPhase.ResolvingLicense)).IsTrue();
            await Assert.That(seen.Exists(p => p.Phase == RunPhase.ParsingCommits)).IsTrue();
            await Assert.That(seen.Exists(p => p.Phase == RunPhase.WritingOutput)).IsTrue();

            await Assert.That(facts.RecordCount).IsEqualTo(2);
            await Assert.That(seams.RawWriter.CallCount).IsEqualTo(1);
            await Assert.That(seams.TrainingWriter.CallCount).IsEqualTo(0);
            await Assert.That(seams.RawWriter.Written.Count).IsEqualTo(2);

            // Capability cache stays lazy on raw runs.
            await Assert.That(seams.Matrix.CallCount).IsEqualTo(0);
            await Assert.That(seams.Matrix.CacheStatusCallCount).IsEqualTo(0);
            await Assert.That(seams.ChatOptions.CallCount).IsEqualTo(0);
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }

    [Test]
    public async Task Training_Dispatch_ReportsBuildingExamples()
    {
        TestSeams seams = CreateSeams();
        // ReSharper disable once RedundantArgumentDefaultValue
        seams.Parser.Commits.Add(SampleCommit("1"));
        seams.Parser.Commits.Add(SampleCommit("2"));
        CliExtractionRunPipeline pipeline = CreatePipeline(seams);

        string tempDir = CreateTempDirectory();
        try
        {
            RunTrainingInput input = ValidTrainingInput(tempDir);

            List<RunProgress> seen = new();
            ExtractionRunFacts facts = await pipeline.RunTrainingAsync(
                input, new LambdaProgress(p => seen.Add(p)), CancellationToken.None);

            await Assert.That(seen.Exists(p => p.Phase == RunPhase.BuildingExamples)).IsTrue();
            await Assert.That(seen.Exists(p => p.Phase == RunPhase.ResolvingLicense)).IsTrue();
            await Assert.That(seen.Exists(p => p.Phase == RunPhase.ParsingCommits)).IsTrue();
            await Assert.That(seen.Exists(p => p.Phase == RunPhase.WritingOutput)).IsTrue();

            await Assert.That(facts.RecordCount).IsEqualTo(2);
            await Assert.That(seams.TrainingWriter.CallCount).IsEqualTo(1);
            await Assert.That(seams.RawWriter.CallCount).IsEqualTo(0);
            await Assert.That(seams.TrainingWriter.Written.Count).IsEqualTo(2);

            // Non-LLM training never pays for capability-cache I/O.
            await Assert.That(seams.Matrix.CallCount).IsEqualTo(0);
            await Assert.That(seams.Matrix.CacheStatusCallCount).IsEqualTo(0);
            await Assert.That(seams.ChatOptions.CallCount).IsEqualTo(0);
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }

    [Test]
    public async Task Facts_Content_OutputPathLicenseCount()
    {
        TestSeams seams = CreateSeams();
        // ReSharper disable once RedundantArgumentDefaultValue
        seams.Parser.Commits.Add(SampleCommit("1"));
        seams.Parser.Commits.Add(SampleCommit("2"));
        seams.Parser.Commits.Add(SampleCommit("3"));
        CliExtractionRunPipeline pipeline = CreatePipeline(seams);

        string tempDir = CreateTempDirectory();
        try
        {
            string outputPath = Path.Combine(tempDir, "out.jsonl");
            RunTrainingInput input = ValidTrainingInput(
                tempDir, license: "MIT", outputPath: outputPath);

            ExtractionRunFacts facts = await pipeline.RunTrainingAsync(
                input, progress: null, CancellationToken.None);

            await Assert.That(facts.OutputPath).IsEqualTo(Path.GetFullPath(outputPath));
            await Assert.That(facts.License).IsEqualTo("MIT");
            await Assert.That(facts.RecordCount).IsEqualTo(3);
            await Assert.That(seams.TrainingWriter.CapturedPath).IsEqualTo(Path.GetFullPath(outputPath));
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }

    [Test]
    public async Task Facts_Raw_DefaultOutputPath_DerivedFromRepoDirectory()
    {
        TestSeams seams = CreateSeams();
        seams.Parser.Commits.Add(SampleCommit());
        CliExtractionRunPipeline pipeline = CreatePipeline(seams);

        string tempDir = CreateTempDirectory();
        try
        {
            RunRawInput input = ValidRawInput(tempDir, license: "MIT", outputPath: "");

            ExtractionRunFacts facts = await pipeline.RunRawAsync(
                input, progress: null, CancellationToken.None);

            string repoName = new DirectoryInfo(tempDir).Name;
            string expected = Path.Combine(new DirectoryInfo(tempDir).FullName, $"{repoName}-commits.jsonl");
            await Assert.That(facts.OutputPath).IsEqualTo(expected);
            await Assert.That(facts.RecordCount).IsEqualTo(1);
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }

    #endregion

    #region Cancellation honoring

    [Test]
    public async Task Raw_Honors_PreCancelledToken()
    {
        TestSeams seams = CreateSeams();
        CliExtractionRunPipeline pipeline = CreatePipeline(seams);

        string tempDir = CreateTempDirectory();
        try
        {
            using CancellationTokenSource cts = new();
            await cts.CancelAsync();

            RunRawInput input = ValidRawInput(tempDir);

            OperationCanceledException? caught = null;
            try
            {
                await pipeline.RunRawAsync(input, progress: null, cts.Token);
            }
            catch (OperationCanceledException ex)
            {
                caught = ex;
            }

            await Assert.That(caught).IsNotNull();
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }

    [Test]
    public async Task Training_Honors_PreCancelledToken()
    {
        TestSeams seams = CreateSeams();
        CliExtractionRunPipeline pipeline = CreatePipeline(seams);

        string tempDir = CreateTempDirectory();
        try
        {
            using CancellationTokenSource cts = new();
            await cts.CancelAsync();

            RunTrainingInput input = ValidTrainingInput(tempDir);

            OperationCanceledException? caught = null;
            try
            {
                await pipeline.RunTrainingAsync(input, progress: null, cts.Token);
            }
            catch (OperationCanceledException ex)
            {
                caught = ex;
            }

            await Assert.That(caught).IsNotNull();
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }

    #endregion

    #region Warnings, validation ordering, and message hygiene

    [Test]
    public async Task Training_RedactionNoneWithLlm_ReportsPiIWarning()
    {
        TestSeams seams = CreateSeams();
        seams.Parser.Commits.Add(SampleCommit());
        CliExtractionRunPipeline pipeline = CreatePipeline(seams);

        string tempDir = CreateTempDirectory();
        try
        {
            // Model is empty, so the run fails at the credential gate after the warning
            // is reported; the warning itself is what is under test.
            RunTrainingInput input = ValidTrainingInput(
                tempDir, redaction: "none", llmAssistantOutput: true);

            List<RunProgress> seen = new();
            ArgumentException? caught = null;
            try
            {
                await pipeline.RunTrainingAsync(
                    input, new LambdaProgress(p => seen.Add(p)), CancellationToken.None);
            }
            catch (ArgumentException ex)
            {
                caught = ex;
            }

            await Assert.That(caught).IsNotNull();
            await Assert.That(seen.Exists(p =>
                p.Phase == RunPhase.Warning &&
                p.Message.Contains("--redaction none") &&
                p.Message.Contains("PII"))).IsTrue();
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }

    [Test]
    public async Task Training_Llm_UnavailableCapabilityData_ReportsWarning()
    {
        TestSeams seams = CreateSeams();
        seams.Parser.Commits.Add(SampleCommit());
        seams.Matrix.Status = CacheStatus.Unavailable;
        CliExtractionRunPipeline pipeline = CreatePipeline(seams);

        string tempDir = CreateTempDirectory();
        try
        {
            RunTrainingInput input = ValidTrainingInput(
                tempDir,
                license: "MIT",
                llmAssistantOutput: true,
                provider: RawValue.From("test-provider", ""),
                model: RawValue.From("test-model", ""));

            // The run later fails while building the chat client (no endpoint in tests);
            // the capability warning is reported before dispatch and is what is under test.
            List<RunProgress> seen = new();
            ArgumentException? caught = null;
            try
            {
                await pipeline.RunTrainingAsync(
                    input, new LambdaProgress(p => seen.Add(p)), CancellationToken.None);
            }
            catch (ArgumentException ex)
            {
                caught = ex;
            }

            await Assert.That(caught).IsNotNull();
            await Assert.That(seams.Matrix.CacheStatusCallCount).IsEqualTo(1);
            await Assert.That(seen.Exists(p =>
                p.Phase == RunPhase.Warning &&
                p.Message.Contains("capability data unavailable"))).IsTrue();
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }

    [Test]
    public async Task Training_Llm_StaleCapabilityData_ReportsWarning()
    {
        TestSeams seams = CreateSeams();
        seams.Parser.Commits.Add(SampleCommit());
        seams.Matrix.Status = CacheStatus.Stale;
        CliExtractionRunPipeline pipeline = CreatePipeline(seams);

        string tempDir = CreateTempDirectory();
        try
        {
            RunTrainingInput input = ValidTrainingInput(
                tempDir,
                license: "MIT",
                llmAssistantOutput: true,
                provider: RawValue.From("test-provider", ""),
                model: RawValue.From("test-model", ""));

            List<RunProgress> seen = new();
            ArgumentException? caught = null;
            try
            {
                await pipeline.RunTrainingAsync(
                    input, new LambdaProgress(p => seen.Add(p)), CancellationToken.None);
            }
            catch (ArgumentException ex)
            {
                caught = ex;
            }

            await Assert.That(caught).IsNotNull();
            await Assert.That(seen.Exists(p =>
                p.Phase == RunPhase.Warning &&
                p.Message.Contains("capability data is stale"))).IsTrue();
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }

    [Test]
    public async Task Training_InvalidTemplate_DoesNotResolveLicense()
    {
        TestSeams seams = CreateSeams();
        seams.Parser.Commits.Add(SampleCommit());
        CliExtractionRunPipeline pipeline = CreatePipeline(seams);

        string tempDir = CreateTempDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "LICENSE.txt"), "license text");

            // License auto-detection is available (file present, endpoint/model set), so
            // before validation was reordered this input paid for an analyzer call
            // before failing on the bad placeholder.
            RunTrainingInput input = ValidTrainingInput(
                tempDir,
                license: "",
                endpoint: RawValue.From("https://example.com", ""),
                model: RawValue.From("test-model", ""),
                systemOverride: "{bogus}");

            ArgumentException? caught = null;
            try
            {
                await pipeline.RunTrainingAsync(input, progress: null, CancellationToken.None);
            }
            catch (ArgumentException ex)
            {
                caught = ex;
            }

            await Assert.That(caught).IsNotNull();
            await Assert.That(seams.License.CallCount).IsEqualTo(0);
            await Assert.That(seams.Parser.CapturedWorkingDir).IsNull();
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }

    [Test]
    public async Task ValidationErrors_AreArgumentExceptions()
    {
        TestSeams seams = CreateSeams();
        CliExtractionRunPipeline pipeline = CreatePipeline(seams);

        string tempDir = CreateTempDirectory();
        try
        {
            List<Exception> errors = new();

            try
            {
                await pipeline.RunTrainingAsync(
                    ValidTrainingInput(tempDir, redaction: "bogus"), progress: null, CancellationToken.None);
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }

            try
            {
                await pipeline.RunTrainingAsync(
                    ValidTrainingInput(tempDir, systemOverride: "{bogus}"), progress: null, CancellationToken.None);
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }

            try
            {
                await pipeline.RunRawAsync(
                    ValidRawInput(tempDir, llmAssistantOutput: true), progress: null, CancellationToken.None);
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }

            try
            {
                await pipeline.RunTrainingAsync(
                    ValidTrainingInput(tempDir, llmOverridePrompt: "{diff}"), progress: null, CancellationToken.None);
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }

            await Assert.That(errors.Count).IsEqualTo(4);

            // ArgumentException parameter names are deliberately included; each
            // scenario is covered individually by its own test above.
            foreach (Exception error in errors)
            {
                await Assert.That(error is ArgumentException).IsTrue();
            }
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }

    #endregion
}
