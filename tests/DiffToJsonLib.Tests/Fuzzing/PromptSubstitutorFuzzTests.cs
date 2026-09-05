using DiffToJsonLib.Training;

namespace DiffToJsonLib.Tests.Fuzzing;

public class PromptSubstitutorFuzzTests
{
    [Test]
    public async Task Substitute_NeverThrows_OnArbitraryInput()
    {
        var testCases = new[]
        {
            ("", "", "", "", "", ""),
            ("{diff}", "val", "", "", "", ""),
            ("{commitMessage}", "", "msg", "", "", ""),
            ("{repoName}", "", "", "repo", "", ""),
            ("{license}", "", "", "", "MIT", ""),
            ("{repoUrl}", "", "", "", "", "https://example.com"),
            ("{unknown}", "d", "m", "r", "l", "u"),
            (new string('x', 1000), "d", "m", "r", "l", "u"),
            ("{diff}{commitMessage}{repoName}{license}{repoUrl}", "d", "m", "r", "l", "u"),
        };

        foreach (var (template, diff, commitMessage, repoName, license, repoUrl) in testCases)
        {
            var result = PromptSubstitutor.Substitute(template, diff, commitMessage, repoName, license, repoUrl);
            await Assert.That(result).IsNotNull();
        }
    }

    [Test]
    public async Task Substitute_ReplacesKnownPlaceholders()
    {
        var result = PromptSubstitutor.Substitute(
            "Diff: {diff}\nMessage: {commitMessage}\nRepo: {repoName}",
            "my-diff",
            "my-message",
            "my-repo",
            "MIT",
            "https://example.com");

        await Assert.That(result).Contains("my-diff");
        await Assert.That(result).Contains("my-message");
        await Assert.That(result).Contains("my-repo");
    }

    [Test]
    public async Task Substitute_EmptyTemplate_ReturnsEmpty()
    {
        var result = PromptSubstitutor.Substitute("", "diff", "msg", "repo", "lic", "url");
        await Assert.That(result).IsEqualTo("");
    }

    [Test]
    public async Task Substitute_PreservesUnknownPlaceholders()
    {
        var result = PromptSubstitutor.Substitute(
            "{unknown} and {diff}",
            "actual-diff",
            "",
            "",
            "",
            "");

        await Assert.That(result).Contains("{unknown}");
        await Assert.That(result).Contains("actual-diff");
    }
}
