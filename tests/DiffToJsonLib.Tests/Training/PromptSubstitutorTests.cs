using DiffToJsonLib.Training;

namespace DiffToJsonLib.Tests.Training;

public class PromptSubstitutorTests
{
    [Test]
    public async Task Substitutes_AllFivePlaceholders()
    {
        string result = PromptSubstitutor.Substitute(
            "{diff} {commitMessage} {repoName} {license} {repoUrl}",
            "the diff",
            "the commit message",
            "my/repo",
            "MIT",
            "https://github.com/my/repo");

        await Assert.That(result).IsEqualTo("the diff the commit message my/repo MIT https://github.com/my/repo");
    }

    [Test]
    public async Task MissingPlaceholders_AreLeftAsIs()
    {
        string result = PromptSubstitutor.Substitute(
            "{diff} {unknown} {commitMessage}",
            "diff content",
            "commit msg",
            "repo",
            "MIT",
            "https://example.com");

        await Assert.That(result).IsEqualTo("diff content {unknown} commit msg");
    }

    [Test]
    public async Task TemplateWithNoPlaceholders_IsReturnedUnchanged()
    {
        string result = PromptSubstitutor.Substitute(
            "Hello world",
            "diff",
            "message",
            "repo",
            "MIT",
            "url");

        await Assert.That(result).IsEqualTo("Hello world");
    }

    [Test]
    public async Task EmptyTemplate_ReturnsEmptyString()
    {
        string result = PromptSubstitutor.Substitute(
            "",
            "diff",
            "message",
            "repo",
            "MIT",
            "url");

        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task EmptyDiff_ReplacesWithEmptyString()
    {
        string result = PromptSubstitutor.Substitute(
            "diff: {diff}",
            "",
            "message",
            "repo",
            "MIT",
            "url");

        await Assert.That(result).IsEqualTo("diff: ");
    }

    [Test]
    public async Task EmptyCommitMessage_ReplacesWithEmptyString()
    {
        string result = PromptSubstitutor.Substitute(
            "msg: {commitMessage}",
            "diff",
            "",
            "repo",
            "MIT",
            "url");

        await Assert.That(result).IsEqualTo("msg: ");
    }

    [Test]
    public async Task EmptyRepoName_ReplacesWithEmptyString()
    {
        string result = PromptSubstitutor.Substitute(
            "repo: {repoName}",
            "diff",
            "message",
            "",
            "MIT",
            "url");

        await Assert.That(result).IsEqualTo("repo: ");
    }

    [Test]
    public async Task EmptyLicense_ReplacesWithEmptyString()
    {
        string result = PromptSubstitutor.Substitute(
            "license: {license}",
            "diff",
            "message",
            "repo",
            "",
            "url");

        await Assert.That(result).IsEqualTo("license: ");
    }

    [Test]
    public async Task EmptyRepoUrl_ReplacesWithEmptyString()
    {
        string result = PromptSubstitutor.Substitute(
            "url: {repoUrl}",
            "diff",
            "message",
            "repo",
            "MIT",
            "");

        await Assert.That(result).IsEqualTo("url: ");
    }
}
