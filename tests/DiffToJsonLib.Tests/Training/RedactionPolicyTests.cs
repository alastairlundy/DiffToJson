using System.Text.RegularExpressions;
using DiffToJsonLib.Models;
using DiffToJsonLib.Redactors;
using DiffToJsonLib.Training;
using Microsoft.Extensions.Compliance.Redaction;

namespace DiffToJsonLib.Tests.Training;

public class RedactionPolicyTests
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

    private static readonly CommitRecord SampleCommit = new(
        Diff: "added secret stuff",
        CommitMessage: "fix: remove secret=1234 from config",
        RepoName: "test/repo",
        License: "MIT",
        RepoUrl: "https://github.com/test/repo");

    private static RedactionPolicy CreatePolicy(RedactionTier tier)
    {
        var redactor = new TestRedactor("secret[a-z0-9=@.]*", "[REDACTED]");
        return new RedactionPolicy(new Dictionary<RedactionTier, Redactor> { [tier] = redactor });
    }

    [Test]
    public async Task RedactCommit_NoneTier_ReturnsInputUnchanged()
    {
        var policy = CreatePolicy(RedactionTier.Message);
        CommitRecord result = policy.Redact(SampleCommit, RedactionTier.None);

        await Assert.That(result).IsSameReferenceAs(SampleCommit);
    }

    [Test]
    public async Task RedactCommit_MissingTier_ReturnsInputUnchanged()
    {
        var redactor = new TestRedactor("secret[a-z0-9=@.]*", "[REDACTED]");
        var policy = new RedactionPolicy(
            new Dictionary<RedactionTier, Redactor> { [RedactionTier.Message] = redactor });

        CommitRecord result = policy.Redact(SampleCommit, RedactionTier.Diff);

        await Assert.That(result).IsSameReferenceAs(SampleCommit);
    }

    [Test]
    public async Task RedactCommit_MessageTier_RedactsOnlyCommitMessage()
    {
        var policy = CreatePolicy(RedactionTier.Message);
        CommitRecord result = policy.Redact(SampleCommit, RedactionTier.Message);

        await Assert.That(result.Diff).IsEqualTo(SampleCommit.Diff);
        await Assert.That(result.CommitMessage).IsEqualTo("fix: remove [REDACTED] from config");
    }

    [Test]
    public async Task RedactCommit_DiffTier_RedactsOnlyDiff()
    {
        var policy = CreatePolicy(RedactionTier.Diff);
        CommitRecord result = policy.Redact(SampleCommit, RedactionTier.Diff);

        await Assert.That(result.Diff).IsEqualTo("added [REDACTED] stuff");
        await Assert.That(result.CommitMessage).IsEqualTo(SampleCommit.CommitMessage);
    }

    [Test]
    public async Task RedactCommit_AllTier_RedactsBoth()
    {
        var policy = CreatePolicy(RedactionTier.All);
        CommitRecord result = policy.Redact(SampleCommit, RedactionTier.All);

        await Assert.That(result.Diff).IsEqualTo("added [REDACTED] stuff");
        await Assert.That(result.CommitMessage).IsEqualTo("fix: remove [REDACTED] from config");
    }

    [Test]
    public async Task RedactCommit_OriginalIsNotMutated()
    {
        var policy = CreatePolicy(RedactionTier.All);
        CommitRecord original = SampleCommit with { };
        _ = policy.Redact(SampleCommit, RedactionTier.All);

        await Assert.That(SampleCommit.Diff).IsEqualTo(original.Diff);
        await Assert.That(SampleCommit.CommitMessage).IsEqualTo(original.CommitMessage);
    }

    [Test]
    public async Task RedactString_NoneTier_ReturnsInputUnchanged()
    {
        var policy = CreatePolicy(RedactionTier.All);
        string result = policy.Redact("hello secret@example.com world", RedactionTier.None);

        await Assert.That(result).IsEqualTo("hello secret@example.com world");
    }

    [Test]
    public async Task RedactString_MissingTier_ReturnsInputUnchanged()
    {
        var redactor = new TestRedactor("secret[a-z0-9=@.]*", "[REDACTED]");
        var policy = new RedactionPolicy(
            new Dictionary<RedactionTier, Redactor> { [RedactionTier.Message] = redactor });

        string result = policy.Redact("keep secret=1234", RedactionTier.Diff);

        await Assert.That(result).IsEqualTo("keep secret=1234");
    }

    [Test]
    public async Task RedactString_PresentTier_RedactsText()
    {
        var policy = CreatePolicy(RedactionTier.All);
        string result = policy.Redact("contact secret@example.com now", RedactionTier.All);

        await Assert.That(result).IsEqualTo("contact [REDACTED] now");
    }
}
