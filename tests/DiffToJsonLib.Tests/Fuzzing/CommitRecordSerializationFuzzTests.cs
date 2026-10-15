using System.Text.Json;
using DiffToJsonLib.Contexts;
using DiffToJsonLib.Models;
using FsCheck;
using FsCheck.Fluent;

namespace DiffToJsonLib.Tests.Fuzzing;

public class CommitRecordSerializationFuzzTests
{
    [Test]
    public void Serialize_NeverThrows_OnArbitraryCommitRecord()
    {
        Gen<string> stringGen = ArbMap.Default.GeneratorFor<string>();

        Gen<CommitRecord> gen = stringGen
            .Where(s => s?.Length <= 1000)
            .Zip(stringGen.Where(s => s?.Length <= 1000), (a, b) => (a, b))
            .Zip(stringGen.Where(s => s?.Length <= 100), (t, c) => (t.a, t.b, c))
            .Zip(stringGen.Where(s => s?.Length <= 100), (t, d) => (t.a, t.b, t.c, d))
            .Zip(stringGen.Where(s => s?.Length <= 200), (t, e) => (t.a, t.b, t.c, t.d, e))
            .Select(t => new CommitRecord(t.a ?? "", t.b ?? "", t.c ?? "", t.d ?? "", t.e ?? ""));

        Prop.ForAll(gen.ToArbitrary(), record =>
        {
            string json = JsonSerializer.Serialize(record, CommitJsonContext.Default.CommitRecord);

            CommitRecord? deserialized = JsonSerializer.Deserialize(json, CommitJsonContext.Default.CommitRecord);

            if (deserialized == null) return false;
            if (deserialized.Diff != record.Diff) return false;
            if (deserialized.CommitMessage != record.CommitMessage) return false;
            if (deserialized.RepoName != record.RepoName) return false;
            if (deserialized.License != record.License) return false;
            if (deserialized.RepoUrl != record.RepoUrl) return false;

            return true;
        }).Check(Config.Default.WithMaxTest(100));
    }

    [Test]
    public async Task Serialize_HandlesEmptyFields()
    {
        CommitRecord record = new CommitRecord("", "", "", "", "");
        string json = JsonSerializer.Serialize(record, CommitJsonContext.Default.CommitRecord);

        await Assert.That(json).IsNotNull();
        await Assert.That(json).IsNotEmpty();

        CommitRecord? deserialized = JsonSerializer.Deserialize(json, CommitJsonContext.Default.CommitRecord);
        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized.Diff).IsEqualTo("");
    }

    [Test]
    public async Task Serialize_HandlesLongFields()
    {
        string longString = new string('x', 100_000);
        CommitRecord record = new CommitRecord(longString, longString, "repo", "MIT", "url");
        string json = JsonSerializer.Serialize(record, CommitJsonContext.Default.CommitRecord);

        await Assert.That(json).IsNotNull();
        await Assert.That(json.Length).IsGreaterThan(100_000);
    }

    [Test]
    public async Task Serialize_HandlesSpecialCharacters()
    {
        CommitRecord record = new CommitRecord(
            "line1\nline2\ttab",
            "message with \"quotes\" and \\backslash",
            "日本語リポジトリ",
            "Apache-2.0",
            "https://example.com/path?q=1&r=2");

        string json = JsonSerializer.Serialize(record, CommitJsonContext.Default.CommitRecord);
        CommitRecord? deserialized = JsonSerializer.Deserialize(json, CommitJsonContext.Default.CommitRecord);

        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized.Diff).IsEqualTo(record.Diff);
        await Assert.That(deserialized.CommitMessage).IsEqualTo(record.CommitMessage);
        await Assert.That(deserialized.RepoName).IsEqualTo(record.RepoName);
    }
}
