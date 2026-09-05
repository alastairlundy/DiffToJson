using System.Text.Json;
using DiffToJsonLib.Contexts;
using DiffToJsonLib.Models;

namespace DiffToJsonLib.Tests.Fuzzing;

public class CommitRecordSerializationFuzzTests
{
    [Test]
    public async Task Serialize_NeverThrows_OnArbitraryCommitRecord()
    {
        var records = new[]
        {
            new CommitRecord("", "", "", "", ""),
            new CommitRecord("diff", "msg", "repo", "MIT", "url"),
            new CommitRecord(new string('x', 10000), new string('y', 10000), "repo", "lic", "url"),
            new CommitRecord("line1\nline2\ttab", "msg with \"quotes\"", "日本語", "Apache-2.0", "https://example.com?q=1&r=2"),
            new CommitRecord("diff\n+added\n-removed", "multi\nline\nmessage", "repo", "", ""),
            new CommitRecord("\x00\x01\x02", "", "", "", ""),
        };

        foreach (var record in records)
        {
            var json = JsonSerializer.Serialize(record, CommitJsonContext.Default.CommitRecord);
            await Assert.That(json).IsNotNull();
            await Assert.That(json).IsNotEmpty();

            var deserialized = JsonSerializer.Deserialize(json, CommitJsonContext.Default.CommitRecord);
            await Assert.That(deserialized).IsNotNull();
            await Assert.That(deserialized!.Diff).IsEqualTo(record.Diff);
            await Assert.That(deserialized.CommitMessage).IsEqualTo(record.CommitMessage);
        }
    }

    [Test]
    public async Task Serialize_HandlesEmptyFields()
    {
        var record = new CommitRecord("", "", "", "", "");
        var json = JsonSerializer.Serialize(record, CommitJsonContext.Default.CommitRecord);

        await Assert.That(json).IsNotNull();
        await Assert.That(json).IsNotEmpty();

        var deserialized = JsonSerializer.Deserialize(json, CommitJsonContext.Default.CommitRecord);
        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized!.Diff).IsEqualTo("");
    }

    [Test]
    public async Task Serialize_HandlesLongFields()
    {
        var longString = new string('x', 100_000);
        var record = new CommitRecord(longString, longString, "repo", "MIT", "url");
        var json = JsonSerializer.Serialize(record, CommitJsonContext.Default.CommitRecord);

        await Assert.That(json).IsNotNull();
        await Assert.That(json.Length).IsGreaterThan(100_000);
    }

    [Test]
    public async Task Serialize_HandlesSpecialCharacters()
    {
        var record = new CommitRecord(
            "line1\nline2\ttab",
            "message with \"quotes\" and \\backslash",
            "日本語リポジトリ",
            "Apache-2.0",
            "https://example.com/path?q=1&r=2");

        var json = JsonSerializer.Serialize(record, CommitJsonContext.Default.CommitRecord);
        var deserialized = JsonSerializer.Deserialize(json, CommitJsonContext.Default.CommitRecord);

        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized!.Diff).IsEqualTo(record.Diff);
        await Assert.That(deserialized.CommitMessage).IsEqualTo(record.CommitMessage);
        await Assert.That(deserialized.RepoName).IsEqualTo(record.RepoName);
    }
}
