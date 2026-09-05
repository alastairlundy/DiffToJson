using DiffToJsonLib.Parsing;

namespace DiffToJsonLib.Tests.Fuzzing;

public class GitLogParserFuzzTests
{
    [Test]
    public async Task ParseAsync_NeverThrows_OnArbitraryInput()
    {
        var parser = new GitLogParser();

        var inputs = new[]
        {
            "",
            "   ",
            "\n\n\n",
            "random text",
            "commit abc123\n\nmessage\n\ndiff --git a/f b/f\n+line",
            new string('x', 10000),
            "commit \x00\x01\x02",
            "commit abc\n\nmsg\n\ndiff --git a/f b/f\n@@ -1 +1 @@\n+add\n-remove"
        };

        foreach (var input in inputs)
        {
            using var reader = new StringReader(input);
            var commits = await parser.ParseAsync(reader, default).ToListAsync();

            foreach (var commit in commits)
            {
                await Assert.That(commit.Message).IsNotNull();
                await Assert.That(commit.Diff).IsNotNull();
            }
        }
    }

    [Test]
    public async Task ParseAsync_ProducesValidCommits_OnDiffInput()
    {
        var diffInput = @"commit abc123

Fix something

diff --git a/file.cs b/file.cs
index 1234567..abcdef0 100644
--- a/file.cs
+++ b/file.cs
@@ -1,3 +1,4 @@
+new line
 unchanged
-old line";

        var parser = new GitLogParser();
        using var reader = new StringReader(diffInput);

        var commits = await parser.ParseAsync(reader, default).ToListAsync();

        await Assert.That(commits).Count().IsEqualTo(1);
        await Assert.That(commits[0].Message).IsNotEmpty();
        await Assert.That(commits[0].Diff).IsNotEmpty();
    }

    [Test]
    public async Task ParseAsync_EmptyOrWhitespaceInput_ProducesNoCommits()
    {
        var inputs = new[] { "", "   ", "\n\n\n", "\r\n\r\n" };
        var parser = new GitLogParser();

        foreach (var input in inputs)
        {
            using var reader = new StringReader(input);
            var commits = await parser.ParseAsync(reader, default).ToListAsync();
            await Assert.That(commits).IsEmpty();
        }
    }
}
