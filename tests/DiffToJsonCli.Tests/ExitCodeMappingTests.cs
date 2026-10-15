using System.IO.Abstractions;
using System.Runtime.CompilerServices;
using DiffToJsonCli.Execution;
using DiffToJsonLib.Abstractions;
using DiffToJsonLib.Models;
using DiffToJsonLib.Reasoning;
using DiffToJsonLib.Redactors;
using DiffToJsonLib.Training;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Compliance.Redaction;
using ModelsDotDevSharp;
using ReasoningEffort = DiffToJsonLib.Reasoning.ReasoningEffort;

namespace DiffToJsonCli.Tests;

/// <summary>
/// Asserts the real exit-code table consumed by <c>Program.cs</c>.
/// <see cref="ExitCode.FromException"/> is the single mapping the adapter uses and
/// these tests assert, so the table cannot drift. Pipeline tests below verify that
/// representative inputs throw the exception kinds the table maps.
/// </summary>
public sealed class ExitCodeMappingTests
{
    private sealed class FakeCommitParser : IGitCommitParser
    {
        public List<CommitRecord> Commits { get; } = new();
        public Exception? Throw { get; set; }

        public Task<CommitRecord[]> ParseCommitsToArrayAsync(
            string repoName, string license, string workingDir, string repoUrl, CancellationToken cancellationToken)
        {
            if (Throw is not null)
            {
                throw Throw;
            }

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

            foreach (CommitRecord commit in Commits)
            {
                yield return commit;
                await Task.CompletedTask;
            }
        }
    }

    private sealed class FakeLicenseAnalyzer : ILicenseAnalyzer
    {
        public Task<string?> AnalyzeLicenseAsync(IFileInfo licenseFile) =>
            Task.FromResult<string?>("MIT");
    }

    private sealed class FakeEffortMatrix : IReasoningEffortMatrix
    {
        public IReadOnlySet<ReasoningEffort> GetSupportedReasoningValues(string model) =>
            new HashSet<ReasoningEffort> { ReasoningEffort.Auto };

        public bool ProducesReasoningOnAuto(string model) => false;

        public AIModelReasoningOptionType? GetReasoningType(string model, string provider) => null;

        public Task<CacheStatus> GetCacheStatusAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CacheStatus.Fresh);
    }

    private sealed class FakeChatOptionsBuilder : IChatOptionsBuilder
    {
        public ChatOptions BuildChatOptions(ReasoningEffort reasoningEffort, string provider, string model) =>
            new();
    }

    private sealed class FakeRawWriter : IDiffJsonFileWriter
    {
        public async Task WriteToJsonFileAsync(
            IAsyncEnumerable<CommitRecord> commits, string filePath, CancellationToken cancellationToken = default)
        {
            await commits.ToListAsync(cancellationToken);
        }

        public Task WriteToJsonFileAsync(
            ICollection<CommitRecord> commits, string filePath, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeTrainingWriter : IDiffTrainingJsonFileWriter
    {
        public async Task WriteToJsonFileAsync(
            IAsyncEnumerable<CommitTrainingRecord> records, string filePath, CancellationToken cancellationToken = default)
        {
            await records.ToListAsync(cancellationToken);
        }
    }

    private static CliExtractionRunPipeline CreatePipeline(FakeCommitParser parser)
    {
        RedactionPolicy policy = new(new Dictionary<RedactionTier, Redactor>());
        return new CliExtractionRunPipeline(
            new FileSystem(),
            parser,
            new FakeLicenseAnalyzer(),
            new FakeEffortMatrix(),
            new FakeChatOptionsBuilder(),
            new TrainingExampleBuilder(policy, new DisabledAssistantMessageGenerator()),
            new FakeRawWriter(),
            new FakeTrainingWriter(),
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

    [Test]
    public async Task ArgumentException_MapsToUsageError()
    {
        await Assert.That(ExitCode.FromException(new ArgumentException("bad usage")))
            .IsEqualTo(ExitCode.UsageError);
    }

    [Test]
    public async Task KeyNotFoundException_MapsToUsageError()
    {
        await Assert.That(ExitCode.FromException(new KeyNotFoundException("unknown preset")))
            .IsEqualTo(ExitCode.UsageError);
    }

    [Test]
    public async Task NotSupportedException_MapsToUsageError()
    {
        await Assert.That(ExitCode.FromException(new NotSupportedException("unsupported effort")))
            .IsEqualTo(ExitCode.UsageError);
    }

    [Test]
    public async Task OperationCanceledException_MapsToCancelled()
    {
        await Assert.That(ExitCode.FromException(new OperationCanceledException()))
            .IsEqualTo(ExitCode.Cancelled);
    }

    [Test]
    public async Task InvalidOperationException_MapsToRunFailure()
    {
        await Assert.That(ExitCode.FromException(new InvalidOperationException("run failure")))
            .IsEqualTo(ExitCode.RunFailure);
    }

    [Test]
    public async Task GenericException_MapsToRunFailure()
    {
        await Assert.That(ExitCode.FromException(new Exception("unexpected")))
            .IsEqualTo(ExitCode.RunFailure);
    }

    [Test]
    public async Task PlaceholderRejection_ThrowsArgumentException_MapsToUsageError()
    {
        FakeCommitParser parser = new();
        parser.Commits.Add(new CommitRecord("diff", "message", "repo", "MIT", "url"));
        CliExtractionRunPipeline pipeline = CreatePipeline(parser);

        string tempDir = CreateTempDirectory();
        try
        {
            RunTrainingInput input = new(
                tempDir,
                "https://github.com/test/repo",
                "MIT",
                "",
                "message",
                "auto",
                false,
                "",
                RawValue.From("", ""),
                RawValue.From("", ""),
                RawValue.From("", ""),
                RawValue.From("", ""),
                "default",
                "{bogus}",
                "");

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
            await Assert.That(ExitCode.FromException(caught!)).IsEqualTo(ExitCode.UsageError);
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }

    [Test]
    public async Task LlmOverridePlaceholderRejection_ThrowsArgumentException_MapsToUsageError()
    {
        FakeCommitParser parser = new();
        parser.Commits.Add(new CommitRecord("diff", "message", "repo", "MIT", "url"));
        CliExtractionRunPipeline pipeline = CreatePipeline(parser);

        string tempDir = CreateTempDirectory();
        try
        {
            RunTrainingInput input = new(
                tempDir,
                "https://github.com/test/repo",
                "MIT",
                "",
                "message",
                "auto",
                true,
                "{bogus}",
                RawValue.From("", ""),
                RawValue.From("", ""),
                RawValue.From("", ""),
                RawValue.From("test-model", ""),
                "default",
                "",
                "");

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
            await Assert.That(ExitCode.FromException(caught!)).IsEqualTo(ExitCode.UsageError);
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }

    [Test]
    public async Task UnknownPreset_ThrowsKeyNotFoundException_MapsToUsageError()
    {
        FakeCommitParser parser = new();
        parser.Commits.Add(new CommitRecord("diff", "message", "repo", "MIT", "url"));
        CliExtractionRunPipeline pipeline = CreatePipeline(parser);

        string tempDir = CreateTempDirectory();
        try
        {
            RunTrainingInput input = new(
                tempDir,
                "https://github.com/test/repo",
                "MIT",
                "",
                "message",
                "auto",
                false,
                "",
                RawValue.From("", ""),
                RawValue.From("", ""),
                RawValue.From("", ""),
                RawValue.From("", ""),
                "nonexistent-preset",
                "",
                "");

            KeyNotFoundException? caught = null;
            try
            {
                await pipeline.RunTrainingAsync(input, progress: null, CancellationToken.None);
            }
            catch (KeyNotFoundException ex)
            {
                caught = ex;
            }

            await Assert.That(caught).IsNotNull();
            await Assert.That(ExitCode.FromException(caught!)).IsEqualTo(ExitCode.UsageError);
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }

    [Test]
    public async Task RunFailure_ThrowsInvalidOperationException_MapsToRunFailure()
    {
        FakeCommitParser parser = new()
        {
            Throw = new InvalidOperationException("simulated run failure"),
        };
        CliExtractionRunPipeline pipeline = CreatePipeline(parser);

        string tempDir = CreateTempDirectory();
        try
        {
            RunTrainingInput input = new(
                tempDir,
                "https://github.com/test/repo",
                "MIT",
                "",
                "message",
                "auto",
                false,
                "",
                RawValue.From("", ""),
                RawValue.From("", ""),
                RawValue.From("", ""),
                RawValue.From("", ""),
                "default",
                "",
                "");

            InvalidOperationException? caught = null;
            try
            {
                await pipeline.RunTrainingAsync(input, progress: null, CancellationToken.None);
            }
            catch (InvalidOperationException ex)
            {
                caught = ex;
            }

            await Assert.That(caught).IsNotNull();
            await Assert.That(ExitCode.FromException(caught!)).IsEqualTo(ExitCode.RunFailure);
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }

    [Test]
    public async Task UsageError_InvalidEffort_ThrowsNotSupportedException_MapsToUsageError()
    {
        FakeCommitParser parser = new();
        CliExtractionRunPipeline pipeline = CreatePipeline(parser);

        string tempDir = CreateTempDirectory();
        try
        {
            RunRawInput input = new(
                tempDir,
                "https://github.com/test/repo",
                "MIT",
                "",
                "message",
                "ultra",
                false,
                "",
                RawValue.From("", ""),
                RawValue.From("", ""),
                RawValue.From("", ""),
                RawValue.From("", ""));

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
            await Assert.That(ExitCode.FromException(caught!)).IsEqualTo(ExitCode.UsageError);
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }

    [Test]
    public async Task Cancellation_MidRun_MapsToCancelled()
    {
        FakeCommitParser parser = new();
        CliExtractionRunPipeline pipeline = CreatePipeline(parser);

        string tempDir = CreateTempDirectory();
        try
        {
            using CancellationTokenSource cts = new();
            await cts.CancelAsync();

            RunRawInput input = new(
                tempDir,
                "https://github.com/test/repo",
                "MIT",
                "",
                "message",
                "auto",
                false,
                "",
                RawValue.From("", ""),
                RawValue.From("", ""),
                RawValue.From("", ""),
                RawValue.From("", ""));

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
            await Assert.That(ExitCode.FromException(caught!)).IsEqualTo(ExitCode.Cancelled);
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }
}
