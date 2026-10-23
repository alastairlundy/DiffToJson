using DiffToJsonLib.Prompts;

namespace DiffToJsonLib.Tests.Prompts;

public class PromptPresetsTests
{
    [Test]
    public async Task ValidateTemplate_AcceptsAllKnownPlaceholders()
    {
        PromptTemplate template = new(
            "System with {diff} and {commitMessage}",
            "User with {repoName}, {license} and {repoUrl}");

        PromptTemplateValidationException? caught = null;
        try
        {
            PromptPresets.ValidateTemplate(template);
        }
        catch (PromptTemplateValidationException ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsNull();
    }

    [Test]
    public async Task ValidateTemplate_AcceptsPlaceholderCaseVariants()
    {
        PromptTemplate template = new("{DIFF} {RepoName}", "{LICENSE}");

        PromptTemplateValidationException? caught = null;
        try
        {
            PromptPresets.ValidateTemplate(template);
        }
        catch (PromptTemplateValidationException ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsNull();
    }

    [Test]
    public async Task ValidateTemplate_RejectsUnknownPlaceholder()
    {
        PromptTemplate template = new("System with {bogus}", string.Empty);

        PromptTemplateValidationException? caught = null;
        try
        {
            PromptPresets.ValidateTemplate(template);
        }
        catch (PromptTemplateValidationException ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsNotNull();
        await Assert.That(caught!.Message).Contains("{bogus}");
    }

    [Test]
    public async Task Get_UnknownPreset_ThrowsKeyNotFoundException()
    {
        KeyNotFoundException? caught = null;
        try
        {
            PromptPresets.Get("unknown-preset");
        }
        catch (KeyNotFoundException ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsNotNull();
    }

    [Test]
    public async Task Get_KnownPresets_AreRegistered()
    {
        await Assert.That(PromptPresets.IsValid("default")).IsTrue();
        await Assert.That(PromptPresets.IsValid("conventional")).IsTrue();
        await Assert.That(PromptPresets.Get("default")).IsNotNull();
        await Assert.That(PromptPresets.Get("conventional")).IsNotNull();
    }
}
