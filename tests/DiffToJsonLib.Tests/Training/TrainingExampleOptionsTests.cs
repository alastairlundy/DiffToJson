using DiffToJsonLib.Prompts;
using DiffToJsonLib.Redactors;
using DiffToJsonLib.Training;
using Microsoft.Extensions.AI;

namespace DiffToJsonLib.Tests.Training;

public class TrainingExampleOptionsTests
{
    [Test]
    public async Task Constructs_WithAllFiveFields()
    {
        var template = new PromptTemplate("system", "user");
        var options = new TrainingExampleOptions(
            template,
            LlmOverridePrompt: "override",
            LlmAssistantOutput: true,
            Tier: RedactionTier.Diff,
            Options: new ChatOptions());

        await Assert.That(options.Template).IsEqualTo(template);
        await Assert.That(options.LlmOverridePrompt).IsEqualTo("override");
        await Assert.That(options.LlmAssistantOutput).IsTrue();
        await Assert.That(options.Tier).IsEqualTo(RedactionTier.Diff);
        await Assert.That(options.Options).IsNotNull();
    }

    [Test]
    public async Task Allows_NullLlmOverridePrompt()
    {
        var options = new TrainingExampleOptions(
            new PromptTemplate("s", "u"),
            LlmOverridePrompt: null,
            LlmAssistantOutput: false,
            Tier: RedactionTier.None,
            Options: new ChatOptions());

        await Assert.That(options.LlmOverridePrompt).IsNull();
    }

    [Test]
    public async Task Allows_LlmAssistantOutputFalse()
    {
        var options = new TrainingExampleOptions(
            new PromptTemplate("s", "u"),
            LlmOverridePrompt: null,
            LlmAssistantOutput: false,
            Tier: RedactionTier.None,
            Options: new ChatOptions());

        await Assert.That(options.LlmAssistantOutput).IsFalse();
    }

    [Test]
    public async Task Record_Equality()
    {
        var chatOptions = new ChatOptions();
        var a = new TrainingExampleOptions(
            new PromptTemplate("s", "u"), "override", true, RedactionTier.Message, chatOptions);
        var b = new TrainingExampleOptions(
            new PromptTemplate("s", "u"), "override", true, RedactionTier.Message, chatOptions);

        await Assert.That(a).IsEqualTo(b);
    }

    [Test]
    public async Task Record_Inequality()
    {
        var a = new TrainingExampleOptions(
            new PromptTemplate("s", "u"), "override", true, RedactionTier.Message, new ChatOptions());
        var b = new TrainingExampleOptions(
            new PromptTemplate("s", "u"), "other", true, RedactionTier.Message, new ChatOptions());

        await Assert.That(a).IsNotEqualTo(b);
    }
}
