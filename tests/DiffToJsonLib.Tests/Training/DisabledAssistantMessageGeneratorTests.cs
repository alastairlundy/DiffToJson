using DiffToJsonLib.Models;
using DiffToJsonLib.Training;
using Microsoft.Extensions.AI;

namespace DiffToJsonLib.Tests.Training;

public class DisabledAssistantMessageGeneratorTests
{
    [Test]
    public async Task GenerateAsync_Returns_AssistantMessageDisabled_WithCommitMessageAsFallback()
    {
        var generator = new DisabledAssistantMessageGenerator();
        var commit = new CommitRecord("diff", "the commit message", "repo", "MIT", "https://example.com");

        AssistantMessageResult result = await generator.GenerateAsync(
            "system", "user", commit, new ChatOptions(), CancellationToken.None);

        await Assert.That(result)
            .IsTypeOf<AssistantMessageResult.AssistantMessageDisabled>();
        await Assert.That(((AssistantMessageResult.AssistantMessageDisabled)result).FallbackContent)
            .IsEqualTo("the commit message");
    }

    [Test]
    public async Task GenerateAsync_Ignores_PromptsAndOptions()
    {
        var generator = new DisabledAssistantMessageGenerator();
        var commit = new CommitRecord("diff", "ignored prompt test", "repo", "MIT", "url");

        AssistantMessageResult result = await generator.GenerateAsync(
            "any system prompt", "any user prompt", commit, new ChatOptions(), CancellationToken.None);

        await Assert.That(((AssistantMessageResult.AssistantMessageDisabled)result).FallbackContent)
            .IsEqualTo("ignored prompt test");
    }
}
