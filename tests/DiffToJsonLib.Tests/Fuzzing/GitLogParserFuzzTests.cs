using DiffToJsonLib.Parsing;
using FsCheck;
using FsCheck.Fluent;

namespace DiffToJsonLib.Tests.Fuzzing;

public class GitLogParserFuzzTests
{
    [Test]
    public void ParseAsync_NeverThrows_OnArbitraryInput()
    {
        GitLogParser parser = new GitLogParser();

        Prop.ForAll(ArbMap.Default.GeneratorFor<string>().Where(s => s?.Length <= 10000).ToArbitrary(), input =>
        {
            using StringReader reader = new StringReader(input ?? "");
            List<RawCommit> commits = parser.ParseAsync(reader, CancellationToken.None).ToListAsync().GetAwaiter().GetResult();

            foreach (RawCommit commit in commits)
            {
                if (string.IsNullOrEmpty(commit.Message) || string.IsNullOrEmpty(commit.Diff)) return false;
            }
            return true;
        }).Check(Config.Default.WithMaxTest(100));
    }

    [Test]
    public async Task ParseAsync_ProducesValidCommits_OnDiffInput()
    {
        string diffInput = @"commit abc123

Fix something

diff --git a/file.cs b/file.cs
index 1234567..abcdef0 100644
--- a/file.cs
+++ b/file.cs
@@ -1,3 +1,4 @@
+new line
 unchanged
-old line";

        GitLogParser parser = new GitLogParser();
        using StringReader reader = new StringReader(diffInput);

        List<RawCommit> commits = await parser.ParseAsync(reader, CancellationToken.None).ToListAsync();

        await Assert.That(commits).Count().IsEqualTo(1);
        await Assert.That(commits[0].Message).IsNotEmpty();
        await Assert.That(commits[0].Diff).IsNotEmpty();
    }

    [Test]
    public async Task ParseAsync_EmptyOrWhitespaceInput_ProducesNoCommits()
    {
        string[] inputs = ["", "   ", "\n\n\n", "\r\n\r\n"];
        GitLogParser parser = new GitLogParser();

        foreach (string input in inputs)
        {
            using StringReader reader = new StringReader(input);
            List<RawCommit> commits = await parser.ParseAsync(reader, CancellationToken.None).ToListAsync();
            await Assert.That(commits).IsEmpty();
        }
    }
}