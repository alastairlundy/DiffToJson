using DiffToJsonLib.Parsing;
using FsCheck;
using FsCheck.Fluent;

namespace DiffToJsonLib.Tests.Fuzzing;

public class GitLogParserFuzzTests
{
    [Test]
    public void ParseAsync_NeverThrows_OnArbitraryInput()
    {
        var parser = new GitLogParser();

        Prop.ForAll(ArbMap.Default.GeneratorFor<string>().Where(s => s?.Length <= 10000).ToArbitrary(), input =>
        {
            using var reader = new StringReader(input ?? "");
            var commits = parser.ParseAsync(reader, default).ToListAsync().GetAwaiter().GetResult();

            foreach (var commit in commits)
            {
                if (commit.Message == null || commit.Diff == null) return false;
            }
            return true;
        }).Check(Config.Default.WithMaxTest(100));
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
