using System.Text.Json;
using DiffToJsonLib.Contexts;
using DiffToJsonLib.Models;
using FsCheck;

namespace DiffToJsonLib.Tests.Fuzzing;

public class CommitRecordSerializationFuzzTests
{
    [Test]
    public async Task Serialize_NeverThrows_OnArbitraryCommitRecord()
    {
        // Use FsCheck to generate bounded arbitrary records
        var gen = Gen.Zip(
            Arb.Default.String().Generator.Where(s => s?.Length <= 1000),
            Arb.Default.String().Generator.Where(s => s?.Length <= 1000),
            Arb.Default.String().Generator.Where(s => s?.Length <= 100),
            Arb.Default.String().Generator.Where(s => s?.Length <= 100),
            Arb.Default.String().Generator.Where(s => s?.Length <= 200)
        ).Map(t => new CommitRecord(t.Item1 ?? "", t.Item2 ?? "", t.Item3 ?? "", t.Item4 ?? "", t.Item5 ?? ""));

        Prop.ForAll(gen.ToArbitrary(), record =>
        {
            var json = JsonSerializer.Serialize(record, CommitJsonContext.Default.CommitRecord);
            
            var deserialized = JsonSerializer.Deserialize(json, CommitJsonContext.Default.CommitRecord);
            
            // Assert all 5 fields
            if (deserialized == null) return false;
            if (deserialized.Diff != record.Diff) return false;
            if (deserialized.CommitMessage != record.CommitMessage) return false;
            if (deserialized.RepoName != record.RepoName) return false;
            if (deserialized.License != record.License) return false;
            if (deserialized.RepoUrl != record.RepoUrl) return false;

            return true;
        }).Check(new FsCheck.Configuration { MaxNbOfTest = 100 });
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
