using DiffToJsonLib.Parsing;

namespace DiffToJsonLib.Tests.Parsing;

public class GitLogParserTests
{
    private static string FixturePath =>
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "tests", "DiffToJsonLib.Tests", "Fixtures", "git-log.txt"
        ));

    [Test]
    public async Task ParseAsync_ParsesMultipleCommits()
    {
        GitLogParser parser = new();
        using StreamReader reader = new(FixturePath);

        List<RawCommit> commits = await parser.ParseAsync(reader, default).ToListAsync();

        await Assert.That(commits).Count().IsEqualTo(2);
    }

    [Test]
    public async Task ParseAsync_FirstCommitMessage_IsCorrect()
    {
        GitLogParser parser = new();
        using StreamReader reader = new(FixturePath);

        List<RawCommit> commits = await parser.ParseAsync(reader, default).ToListAsync();

        await Assert.That(commits[0].Message).IsEqualTo("Add user authentication module\r\n\r\n    Implement login and registration endpoints with JWT token generation.");
    }

    [Test]
    public async Task ParseAsync_SecondCommitMessage_IsCorrect()
    {
        GitLogParser parser = new();
        using StreamReader reader = new(FixturePath);

        List<RawCommit> commits = await parser.ParseAsync(reader, default).ToListAsync();

        await Assert.That(commits[1].Message).IsEqualTo("Fix null reference in data pipeline");
    }

    [Test]
    public async Task ParseAsync_FirstCommitDiff_ContainsExpectedContent()
    {
        GitLogParser parser = new();
        using StreamReader reader = new(FixturePath);

        List<RawCommit> commits = await parser.ParseAsync(reader, default).ToListAsync();

        await Assert.That(commits[0].Diff).Contains("LoginHandler");
        await Assert.That(commits[0].Diff).Contains("RegisterHandler");
    }

    [Test]
    public async Task ParseAsync_SecondCommitDiff_ContainsExpectedContent()
    {
        GitLogParser parser = new();
        using StreamReader reader = new(FixturePath);

        List<RawCommit> commits = await parser.ParseAsync(reader, default).ToListAsync();

        await Assert.That(commits[1].Diff).Contains("DataPipeline");
        await Assert.That(commits[1].Diff).Contains("DefaultConfig");
    }

    [Test]
    public async Task ParseAsync_EmptyInput_ReturnsNoCommits()
    {
        GitLogParser parser = new();
        using StringReader reader = new(string.Empty);

        List<RawCommit> commits = await parser.ParseAsync(reader, default).ToListAsync();

        await Assert.That(commits).IsEmpty();
    }
}
