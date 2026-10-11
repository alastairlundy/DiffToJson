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
/// Documents and asserts the adapter exit-code table from
/// <c>src/DiffToJsonCli/Program.cs</c>:
/// 0 on success, 2 on usage errors (<see cref="ArgumentException"/> including
/// translated placeholder rejections plus <see cref="KeyNotFoundException"/>
/// for unknown presets), 1 on run failures and unexpected errors.
/// The mapper below mirrors the <c>SetAction</c> catch blocks verbatim so the
/// table stays asserted without driving the whole command.
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
        public Task<string?> AnalyzeLicenseAsync(FileInfo licenseFile) =>
            Task.FromResult<string?>("MIT");
    }

    private sealed class FakeEffortMatrix : IReasoningEffortMatrix
    {
        public IReadOnlySet<ReasoningEffort> GetSupportedReasoningValues(string model) =>
            new HashSet<ReasoningEffort> { ReasoningEffort.Auto };

        public bool ProducesReasoningOnAuto(string model) => false;

        public AIModelReasoningOptionType? GetReasoningType(string model, string provider) => null;
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

    /// <summary>
    /// Mirrors <c>Program.cs SetAction</c> catch mapping:
    /// <see cref="ArgumentException"/> plus <see cref="KeyNotFoundException"/>
    /// exit 2; any other <see cref="Exception"/> exits 1.
    /// </summary>
    private static int MapToExitCode(Exception ex) => ex switch
    {
        ArgumentException => 2,
        KeyNotFoundException => 2,
        _ => 1,
    };

    private static CliExtractionRunPipeline CreatePipeline(FakeCommitParser parser)
    {
        return new CliExtractionRunPipeline(
            parser,
            new FakeLicenseAnalyzer(),
            new FakeEffortMatrix(),
            new FakeChatOptionsBuilder(),
            new TrainingExampleBuilder(
                new RedactionPolicy(new Dictionary<RedactionTier, Redactor>()),
                new DisabledAssistantMessageGenerator()),
            new FakeRawWriter(),
            new FakeTrainingWriter());
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        return path;
    }

    [Test]
    public async Task ArgumentException_MapsToExitCode2()
    {
        await Assert.That(MapToExitCode(new ArgumentException("bad usage"))).IsEqualTo(2);
    }

    [Test]
    public async Task KeyNotFoundException_MapsToExitCode2()
    {
        await Assert.That(MapToExitCode(new KeyNotFoundException("unknown preset"))).IsEqualTo(2);
    }

    [Test]
    public async Task InvalidOperationException_MapsToExitCode1()
    {
        await Assert.That(MapToExitCode(new InvalidOperationException("run failure"))).IsEqualTo(1);
    }

    [Test]
    public async Task GenericException_MapsToExitCode1()
    {
        await Assert.That(MapToExitCode(new Exception("unexpected"))).IsEqualTo(1);
    }

    [Test]
    public async Task PlaceholderRejection_ThrowsArgumentException_MapsToExitCode2()
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
            await Assert.That(MapToExitCode(caught!)).IsEqualTo(2);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public async Task LlmOverridePlaceholderRejection_ThrowsArgumentException_MapsToExitCode2()
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
            await Assert.That(MapToExitCode(caught!)).IsEqualTo(2);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public async Task UnknownPreset_ThrowsKeyNotFoundException_MapsToExitCode2()
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
            await Assert.That(MapToExitCode(caught!)).IsEqualTo(2);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public async Task RunFailure_ThrowsInvalidOperationException_MapsToExitCode1()
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
            await Assert.That(MapToExitCode(caught!)).IsEqualTo(1);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public async Task UsageError_InvalidEffort_MapsToExitCode2()
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
            await Assert.That(MapToExitCode(caught!)).IsEqualTo(2);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
