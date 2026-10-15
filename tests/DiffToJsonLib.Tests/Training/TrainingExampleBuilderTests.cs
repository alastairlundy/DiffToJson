using System.Text.RegularExpressions;
using DiffToJsonLib.Models;
using DiffToJsonLib.Prompts;
using DiffToJsonLib.Redactors;
using DiffToJsonLib.Training;
using DiffToJsonLib.Training.Abstractions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Compliance.Redaction;

namespace DiffToJsonLib.Tests.Training;

public sealed class TrainingExampleBuilderTests
{
    private sealed class TestRedactor(string pattern, string replacement) : Redactor
    {
        private readonly Regex _regex = new(pattern);

        public override string Redact(string? source) =>
            source is null ? string.Empty : _regex.Replace(source, replacement);

        public override int GetRedactedLength(ReadOnlySpan<char> source) =>
            replacement.Length;

        public override int Redact(ReadOnlySpan<char> source, Span<char> destination)
        {
            string redacted = Redact(source.ToString());
            redacted.AsSpan().CopyTo(destination);
            return redacted.Length;
        }
    }

    private sealed class FakeAssistantMessageGenerator(AssistantMessageResult result) : IAssistantMessageGenerator
    {
        public Task<AssistantMessageResult> GenerateAsync(
            string systemPrompt,
            string userPrompt,
            CommitRecord redactedCommit,
            ChatOptions options,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(result);
        }
    }

    private static readonly CommitRecord SampleCommit = new(
        Diff: "diff content secret=1234",
        CommitMessage: "commit: fix secret=5678",
        RepoName: "my/repo",
        License: "MIT",
        RepoUrl: "https://github.com/my/repo");

    private static readonly PromptTemplate SampleTemplate = new(
        System: "You are a helpful assistant. Repo: {repoName} License: {license}",
        User: "Diff: {diff}\nMessage: {commitMessage}");

    private static RedactionPolicy CreateRedactionPolicy()
    {
        TestRedactor redactor = new TestRedactor("secret[a-z0-9=@.]*", "[REDACTED]");
        return new RedactionPolicy(new Dictionary<RedactionTier, Redactor>
        {
            [RedactionTier.All] = redactor
        });
    }

    [Test]
    public async Task BuildAsync_WithoutLlmOutput_UsesCommitMessageAsAssistantContent()
    {
        RedactionPolicy redactor = CreateRedactionPolicy();
        FakeAssistantMessageGenerator generator = new FakeAssistantMessageGenerator(
            new AssistantMessageResult.AssistantMessageDisabled("ignored"));
        TrainingExampleBuilder builder = new TrainingExampleBuilder(redactor, generator);
        TrainingExampleOptions options = new TrainingExampleOptions(
            SampleTemplate, null, false, RedactionTier.All, new ChatOptions());

        IAsyncEnumerable<CommitRecord> source = AsyncEnumerable([SampleCommit]);
        List<CommitTrainingRecord> records = await builder.BuildAsync(source, options, CancellationToken.None).ToListAsync(cancellationToken: CancellationToken.None);

        await Assert.That(records).HasSingleItem();
        CommitTrainingRecord record = records[0];
        await Assert.That(record.Messages.Length).IsEqualTo(3);
        await Assert.That(record.Messages[0].Role).IsEqualTo("system");
        await Assert.That(record.Messages[1].Role).IsEqualTo("user");
        await Assert.That(record.Messages[2].Role).IsEqualTo("assistant");
        await Assert.That(record.Messages[2].Content).IsEqualTo("commit: fix [REDACTED]");
        await Assert.That(record.OriginalAssistantMessage).IsNull();
    }

    [Test]
    public async Task BuildAsync_WithLlmOutputAndGeneratedResult_UsesGeneratedContent()
    {
        RedactionPolicy redactor = CreateRedactionPolicy();
        FakeAssistantMessageGenerator generator = new FakeAssistantMessageGenerator(
            new AssistantMessageResult.AssistantMessageGenerated(
                "generated response", "original response"));
        TrainingExampleBuilder builder = new TrainingExampleBuilder(redactor, generator);
        TrainingExampleOptions options = new TrainingExampleOptions(
            SampleTemplate, null, true, RedactionTier.All, new ChatOptions());

        IAsyncEnumerable<CommitRecord> source = AsyncEnumerable([SampleCommit]);
        List<CommitTrainingRecord> records = await builder.BuildAsync(source, options, CancellationToken.None).ToListAsync(cancellationToken: CancellationToken.None);

        await Assert.That(records).HasSingleItem();
        CommitTrainingRecord record = records[0];
        await Assert.That(record.Messages[2].Content).IsEqualTo("generated response");
        await Assert.That(record.OriginalAssistantMessage).IsEqualTo("original response");
    }

    [Test]
    public async Task BuildAsync_WithLlmOutputAndDisabledResult_UsesFallbackAndNoOriginal()
    {
        RedactionPolicy redactor = CreateRedactionPolicy();
        FakeAssistantMessageGenerator generator = new FakeAssistantMessageGenerator(
            new AssistantMessageResult.AssistantMessageDisabled("fallback content"));
        TrainingExampleBuilder builder = new TrainingExampleBuilder(redactor, generator);
        TrainingExampleOptions options = new TrainingExampleOptions(
            SampleTemplate, null, true, RedactionTier.All, new ChatOptions());

        IAsyncEnumerable<CommitRecord> source = AsyncEnumerable([SampleCommit]);
        List<CommitTrainingRecord> records = await builder.BuildAsync(source, options, CancellationToken.None).ToListAsync(cancellationToken: CancellationToken.None);

        await Assert.That(records).HasSingleItem();
        CommitTrainingRecord record = records[0];
        await Assert.That(record.Messages[2].Content).IsEqualTo("fallback content");
        await Assert.That(record.OriginalAssistantMessage).IsNull();
    }

    [Test]
    public async Task BuildAsync_WithLlmOutputAndFailedResultWithFallback_UsesFallbackAndSetsOriginal()
    {
        RedactionPolicy redactor = CreateRedactionPolicy();
        FakeAssistantMessageGenerator generator = new FakeAssistantMessageGenerator(
            new AssistantMessageResult.AssistantMessageAttemptedAndFailed(
                "fallback after failure", "original attempted"));
        TrainingExampleBuilder builder = new TrainingExampleBuilder(redactor, generator);
        TrainingExampleOptions options = new TrainingExampleOptions(
            SampleTemplate, null, true, RedactionTier.All, new ChatOptions());

        IAsyncEnumerable<CommitRecord> source = AsyncEnumerable([SampleCommit]);
        List<CommitTrainingRecord> records = await builder.BuildAsync(source, options, CancellationToken.None).ToListAsync(cancellationToken: CancellationToken.None);

        await Assert.That(records).HasSingleItem();
        CommitTrainingRecord record = records[0];
        await Assert.That(record.Messages[2].Content).IsEqualTo("fallback after failure");
        await Assert.That(record.OriginalAssistantMessage).IsEqualTo("original attempted");
    }

    [Test]
    public async Task BuildAsync_WithLlmOutputAndFailedResultWithoutFallback_UsesOriginalAsFallback()
    {
        RedactionPolicy redactor = CreateRedactionPolicy();
        FakeAssistantMessageGenerator generator = new FakeAssistantMessageGenerator(
            new AssistantMessageResult.AssistantMessageAttemptedAndFailed(
                null, "original attempted"));
        TrainingExampleBuilder builder = new TrainingExampleBuilder(redactor, generator);
        TrainingExampleOptions options = new TrainingExampleOptions(
            SampleTemplate, null, true, RedactionTier.All, new ChatOptions());

        IAsyncEnumerable<CommitRecord> source = AsyncEnumerable([SampleCommit]);
        List<CommitTrainingRecord> records = await builder.BuildAsync(source, options, CancellationToken.None).ToListAsync(cancellationToken: CancellationToken.None);

        await Assert.That(records).HasSingleItem();
        CommitTrainingRecord record = records[0];
        await Assert.That(record.Messages[2].Content).IsEqualTo("original attempted");
        await Assert.That(record.OriginalAssistantMessage).IsEqualTo("original attempted");
    }

    [Test]
    public async Task BuildAsync_WithLlmOverridePrompt_UsesOverrideAsUserPrompt()
    {
        RedactionPolicy redactor = CreateRedactionPolicy();
        string? capturedUserPrompt = null;
        AsyncLocalCapturingGenerator generator = new AsyncLocalCapturingGenerator(p =>
        {
            capturedUserPrompt = p;
            return new AssistantMessageResult.AssistantMessageGenerated("response", "original");
        });
        TrainingExampleBuilder builder = new TrainingExampleBuilder(redactor, generator);
        TrainingExampleOptions options = new TrainingExampleOptions(
            SampleTemplate,
            "Custom prompt: {commitMessage} in {repoName}",
            true,
            RedactionTier.All,
            new ChatOptions());

        IAsyncEnumerable<CommitRecord> source = AsyncEnumerable([SampleCommit]);
        List<CommitTrainingRecord> records = await builder.BuildAsync(source, options, CancellationToken.None).ToListAsync(cancellationToken: CancellationToken.None);

        await Assert.That(records).HasSingleItem();
        await Assert.That(capturedUserPrompt).IsEqualTo("Custom prompt: commit: fix [REDACTED] in my/repo");
    }

    [Test]
    public async Task BuildAsync_AppliesRedactionBeforeSubstitution()
    {
        RedactionPolicy redactor = CreateRedactionPolicy();
        FakeAssistantMessageGenerator generator = new FakeAssistantMessageGenerator(
            new AssistantMessageResult.AssistantMessageGenerated("response", "original"));
        TrainingExampleBuilder builder = new TrainingExampleBuilder(redactor, generator);
        TrainingExampleOptions options = new TrainingExampleOptions(
            new PromptTemplate("System", "Diff: {diff}"),
            null,
            true,
            RedactionTier.All,
            new ChatOptions());

        IAsyncEnumerable<CommitRecord> source = AsyncEnumerable([SampleCommit]);
        List<CommitTrainingRecord> records = await builder.BuildAsync(source, options, CancellationToken.None).ToListAsync(cancellationToken: CancellationToken.None);

        await Assert.That(records).HasSingleItem();
        await Assert.That(records[0].Messages[1].Content).IsEqualTo("Diff: diff content [REDACTED]");
    }

    [Test]
    public async Task BuildAsync_SetsProvenanceAndLegalFromRedactedCommit()
    {
        RedactionPolicy redactor = CreateRedactionPolicy();
        FakeAssistantMessageGenerator generator = new FakeAssistantMessageGenerator(
            new AssistantMessageResult.AssistantMessageGenerated("response", "original"));
        TrainingExampleBuilder builder = new TrainingExampleBuilder(redactor, generator);
        TrainingExampleOptions options = new TrainingExampleOptions(
            SampleTemplate, null, false, RedactionTier.None, new ChatOptions());

        IAsyncEnumerable<CommitRecord> source = AsyncEnumerable([SampleCommit]);
        List<CommitTrainingRecord> records = await builder.BuildAsync(source, options, CancellationToken.None).ToListAsync(cancellationToken: CancellationToken.None);

        CommitTrainingRecord record = records[0];
        await Assert.That(record.Provenance.RepoName).IsEqualTo("my/repo");
        await Assert.That(record.Provenance.RepoUrl).IsEqualTo("https://github.com/my/repo");
        await Assert.That(record.Legal.License).IsEqualTo("MIT");
    }

    [Test]
    public async Task BuildAsync_RedactsMessageWithSpecifiedTier()
    {
        TestRedactor testRedactor = new TestRedactor("secret[a-z0-9=@.]*", "[REDACTED]");
        RedactionPolicy redactor = new RedactionPolicy(new Dictionary<RedactionTier, Redactor>
        {
            [RedactionTier.Message] = testRedactor
        });
        FakeAssistantMessageGenerator generator = new FakeAssistantMessageGenerator(
            new AssistantMessageResult.AssistantMessageGenerated("response", "original"));
        TrainingExampleBuilder builder = new TrainingExampleBuilder(redactor, generator);
        TrainingExampleOptions options = new TrainingExampleOptions(
            new PromptTemplate("System", "Message: {commitMessage}"),
            null,
            true,
            RedactionTier.Message,
            new ChatOptions());

        CommitRecord commit = new CommitRecord(
            "diff with secret=9999",
            "commit with secret=1234",
            "repo", "MIT", "url");

        IAsyncEnumerable<CommitRecord> source = AsyncEnumerable([commit]);
        List<CommitTrainingRecord> records = await builder.BuildAsync(source, options, CancellationToken.None).ToListAsync(cancellationToken: CancellationToken.None);

        await Assert.That(records[0].Messages[1].Content).IsEqualTo("Message: commit with [REDACTED]");
    }

    [Test]
    public async Task BuildAsync_WithNoRedactionTier_PassesOriginalContentToSubstitution()
    {
        RedactionPolicy redactor = CreateRedactionPolicy();
        FakeAssistantMessageGenerator generator = new FakeAssistantMessageGenerator(
            new AssistantMessageResult.AssistantMessageGenerated("response", "original"));
        TrainingExampleBuilder builder = new TrainingExampleBuilder(redactor, generator);
        TrainingExampleOptions options = new TrainingExampleOptions(
            new PromptTemplate("System", "Diff: {diff}"),
            null,
            true,
            RedactionTier.None,
            new ChatOptions());

        IAsyncEnumerable<CommitRecord> source = AsyncEnumerable([SampleCommit]);
        List<CommitTrainingRecord> records = await builder.BuildAsync(source, options, CancellationToken.None).ToListAsync(cancellationToken: CancellationToken.None);

        await Assert.That(records[0].Messages[1].Content).IsEqualTo("Diff: diff content secret=1234");
    }

    [Test]
    public async Task BuildAsync_ProcessesMultipleCommits()
    {
        RedactionPolicy redactor = CreateRedactionPolicy();
        FakeAssistantMessageGenerator generator = new FakeAssistantMessageGenerator(
            new AssistantMessageResult.AssistantMessageGenerated("response", "original"));
        TrainingExampleBuilder builder = new TrainingExampleBuilder(redactor, generator);
        TrainingExampleOptions options = new TrainingExampleOptions(
            SampleTemplate, null, true, RedactionTier.None, new ChatOptions());

        CommitRecord commit2 = SampleCommit with { CommitMessage = "second commit" };
        IAsyncEnumerable<CommitRecord> source = AsyncEnumerable([SampleCommit, commit2]);
        List<CommitTrainingRecord> records = await builder.BuildAsync(source, options, CancellationToken.None).ToListAsync(cancellationToken: CancellationToken.None);

        await Assert.That(records.Count).IsEqualTo(2);
    }

    private static async IAsyncEnumerable<T> AsyncEnumerable<T>(IEnumerable<T> items)
    {
        foreach (T item in items)
        {
            yield return item;
            await Task.CompletedTask;
        }
    }

    private sealed class AsyncLocalCapturingGenerator : IAssistantMessageGenerator
    {
        private readonly Func<string, AssistantMessageResult> _callback;

        public AsyncLocalCapturingGenerator(Func<string, AssistantMessageResult> callback)
        {
            _callback = callback;
        }

        public Task<AssistantMessageResult> GenerateAsync(
            string systemPrompt,
            string userPrompt,
            CommitRecord redactedCommit,
            ChatOptions options,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_callback(userPrompt));
        }
    }
}
