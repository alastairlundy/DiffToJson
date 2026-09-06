using DiffToJsonLib.Redactors;
using FsCheck;
using FsCheck.Fluent;

namespace DiffToJsonLib.Tests.Fuzzing;

public class RegexPiiRedactorFuzzTests
{
    [Test]
    public void Redact_NeverThrows_OnFsCheckGeneratedInputs()
    {
        var redactor = new RegexPiiRedactor();

        Prop.ForAll(ArbMap.Default.GeneratorFor<string>().ToArbitrary(), input =>
        {
            var result = redactor.Redact(input ?? "");
            if (result == null) return false;

            ReadOnlySpan<char> sourceSpan = (input ?? "").AsSpan();
            Span<char> destSpan = stackalloc char[Math.Max(1024, (input ?? "").Length * 2)];
            int written = redactor.Redact(sourceSpan, destSpan);
            if (written < 0) return false;
            var spanResult = destSpan.Slice(0, written).ToString();
            if (spanResult == null) return false;

            return true;
        }).Check(Config.Default.WithMaxTest(100));
    }

    [Test]
    public async Task Redact_NeverThrows_OnFixedInputs()
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

            VerifySpanOverload(redactor, input);
        }
    }

    private static void VerifySpanOverload(RegexPiiRedactor redactor, string input)
    {
        ReadOnlySpan<char> sourceSpan = input.AsSpan();
        Span<char> destSpan = stackalloc char[Math.Max(1024, input.Length * 2)];
        int written = redactor.Redact(sourceSpan, destSpan);

        if (written < 0) throw new Exception("Written length cannot be negative");
        var redactedSpanResult = destSpan.Slice(0, written).ToString();
        if (redactedSpanResult == null) throw new Exception("Redacted result cannot be null");
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
