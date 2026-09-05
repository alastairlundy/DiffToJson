using DiffToJsonLib.Redactors;

namespace DiffToJsonLib.Tests.Fuzzing;

public class RegexPiiRedactorFuzzTests
{
    [Test]
    public async Task Redact_NeverThrows_OnArbitraryInput()
    {
        var redactor = new RegexPiiRedactor();
        var strings = new[]
        {
            "",
            "hello world",
            "user@example.com",
            "<admin@test.org>",
            new string('a', 10000),
            "no emails here",
            "email: test@domain.com and another@foo.bar",
            "\x00\x01\x02",
            "string with\nnewlines\tand\ttabs",
            "<a@b.c>",
            "a@b.c",
        };

        foreach (var input in strings)
        {
            var result = redactor.Redact(input);
            await Assert.That(result).IsNotNull();
        }
    }

    [Test]
    public async Task Redact_ReturnsNonEmpty_ForNonNullInput()
    {
        var redactor = new RegexPiiRedactor();
        var nonEmptyInputs = new[]
        {
            "hello",
            "test@email.com",
            "a@b.c",
            new string('x', 1000),
        };

        foreach (var input in nonEmptyInputs)
        {
            var result = redactor.Redact(input);
            await Assert.That(result).IsNotNull();
            await Assert.That(result).IsNotEmpty();
        }
    }

    [Test]
    public async Task Redact_Null_ReturnsEmpty()
    {
        var redactor = new RegexPiiRedactor();
        var result = redactor.Redact(null);
        await Assert.That(result).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task Redact_RemovesEmailAddresses()
    {
        var redactor = new RegexPiiRedactor();
        var result = redactor.Redact("Contact me at user@example.com or <admin@test.org>");
        await Assert.That(result).DoesNotContain("user@example.com");
        await Assert.That(result).DoesNotContain("admin@test.org");
        await Assert.That(result).Contains("REDACTED");
    }
}
