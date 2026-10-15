using DiffToJsonLib.Training;
using FsCheck;
using FsCheck.Fluent;

namespace DiffToJsonLib.Tests.Fuzzing;

public class PromptSubstitutorFuzzTests
{
    [Test]
    public void Substitute_NeverThrows_OnFsCheckGeneratedTemplates()
    {
        Gen<string> gen = ArbMap.Default.GeneratorFor<string>().Where(s => s?.Length <= 10000);

        Prop.ForAll(gen.ToArbitrary(), template =>
        {
            string result = PromptSubstitutor.Substitute(
                template ?? "", "d", "m", "r", "l", "u");
            return !string.IsNullOrEmpty(result);
        }).Check(Config.Default.WithMaxTest(100));
    }

    [Test]
    public void Substitute_NeverThrows_OnFsCheckGeneratedValues()
    {
        Gen<string> gen = ArbMap.Default.GeneratorFor<string>().Where(s => s?.Length <= 1000);

        Prop.ForAll(gen.ToArbitrary(), diff =>
        {
            string result = PromptSubstitutor.Substitute(
                "{diff}{commitMessage}{repoName}", diff ?? "", "msg", "repo", "MIT", "url");
            return !string.IsNullOrEmpty(result);
        }).Check(Config.Default.WithMaxTest(100));
    }

    [Test]
    public async Task Substitute_NeverThrows_OnFixedCases()
    {
        (string, string, string, string, string, string)[] testCases =
        [
            ("", "", "", "", "", ""),
            ("{diff}", "val", "", "", "", ""),
            ("{commitMessage}", "", "msg", "", "", ""),
            ("{repoName}", "", "", "repo", "", ""),
            ("{license}", "", "", "", "MIT", ""),
            ("{repoUrl}", "", "", "", "", "https://example.com"),
            ("{unknown}", "d", "m", "r", "l", "u"),
            (new string('x', 1000), "d", "m", "r", "l", "u"),
            ("{diff}{commitMessage}{repoName}{license}{repoUrl}", "d", "m", "r", "l", "u")
        ];

        foreach ((string template, string diff, string commitMessage, string repoName, string license, string repoUrl) in testCases)
        {
            string result = PromptSubstitutor.Substitute(template, diff, commitMessage, repoName, license, repoUrl);
            await Assert.That(result).IsNotNull();
        }
    }

    [Test]
    public async Task Substitute_ReplacesKnownPlaceholders()
    {
        string result = PromptSubstitutor.Substitute(
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
        string result = PromptSubstitutor.Substitute("", "diff", "msg", "repo", "lic", "url");
        await Assert.That(result).IsEqualTo("");
    }

    [Test]
    public async Task Substitute_PreservesUnknownPlaceholders()
    {
        string result = PromptSubstitutor.Substitute(
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
