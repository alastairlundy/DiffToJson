using System.Reflection;
using DiffToJsonLib.Training;

namespace DiffToJsonLib.Tests.Training;

public class AssistantMessageResultTests
{
    [Test]
    public async Task AssistantMessageGenerated_ConstructsWithProperties()
    {
        var result = new AssistantMessageResult.AssistantMessageGenerated("the content", "the original");

        await Assert.That(result.Content).IsEqualTo("the content");
        await Assert.That(result.OriginalAssistantMessage).IsEqualTo("the original");
    }

    [Test]
    public async Task AssistantMessageDisabled_ConstructsWithFallbackContent()
    {
        var result = new AssistantMessageResult.AssistantMessageDisabled("fallback");

        await Assert.That(result.FallbackContent).IsEqualTo("fallback");
    }

    [Test]
    public async Task AssistantMessageAttemptedAndFailed_ConstructsWithProperties()
    {
        var result = new AssistantMessageResult.AssistantMessageAttemptedAndFailed("fallback", "original");

        await Assert.That(result.FallbackContent).IsEqualTo("fallback");
        await Assert.That(result.OriginalAssistantMessage).IsEqualTo("original");
    }

    [Test]
    public async Task AssistantMessageAttemptedAndFailed_AllowsNullFallback()
    {
        var result = new AssistantMessageResult.AssistantMessageAttemptedAndFailed(null, "original");

        await Assert.That(result.FallbackContent).IsNull();
        await Assert.That(result.OriginalAssistantMessage).IsEqualTo("original");
    }

    [Test]
    public async Task BaseType_HasPrivateConstructor()
    {
        ConstructorInfo[] ctors = typeof(AssistantMessageResult)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic);

        ConstructorInfo privateCtor = ctors.Single(c => c.IsPrivate);

        await Assert.That(privateCtor.IsPrivate).IsTrue();
    }

    [Test]
    public async Task PatternMatch_IsExhaustiveOverThreeVariants()
    {
        AssistantMessageResult[] results =
        [
            new AssistantMessageResult.AssistantMessageGenerated("content", "original"),
            new AssistantMessageResult.AssistantMessageDisabled("fallback"),
            new AssistantMessageResult.AssistantMessageAttemptedAndFailed(null, "original")
        ];

        int generated = 0, disabled = 0, failed = 0;

        foreach (AssistantMessageResult result in results)
        {
            switch (result)
            {
                case AssistantMessageResult.AssistantMessageGenerated:
                    generated++;
                    break;
                case AssistantMessageResult.AssistantMessageDisabled:
                    disabled++;
                    break;
                case AssistantMessageResult.AssistantMessageAttemptedAndFailed:
                    failed++;
                    break;
            }
        }

        await Assert.That(generated).IsEqualTo(1);
        await Assert.That(disabled).IsEqualTo(1);
        await Assert.That(failed).IsEqualTo(1);
    }

    [Test]
    public async Task AllVariants_AreAssignableToBaseType()
    {
        AssistantMessageResult generated = new AssistantMessageResult.AssistantMessageGenerated("c", "o");
        AssistantMessageResult disabled = new AssistantMessageResult.AssistantMessageDisabled("f");
        AssistantMessageResult failed = new AssistantMessageResult.AssistantMessageAttemptedAndFailed("f", "o");

        await Assert.That(generated).IsTypeOf<AssistantMessageResult.AssistantMessageGenerated>();
        await Assert.That(disabled).IsTypeOf<AssistantMessageResult.AssistantMessageDisabled>();
        await Assert.That(failed).IsTypeOf<AssistantMessageResult.AssistantMessageAttemptedAndFailed>();
    }

    [Test]
    public async Task AssistantMessageGenerated_RecordsEquality()
    {
        var a = new AssistantMessageResult.AssistantMessageGenerated("content", "original");
        var b = new AssistantMessageResult.AssistantMessageGenerated("content", "original");

        await Assert.That(a).IsEqualTo(b);
    }

    [Test]
    public async Task AssistantMessageGenerated_InequalityOnDifferentContent()
    {
        var a = new AssistantMessageResult.AssistantMessageGenerated("content a", "original");
        var b = new AssistantMessageResult.AssistantMessageGenerated("content b", "original");

        await Assert.That(a).IsNotEqualTo(b);
    }
}
