using DiffToJsonLib.Redactors;
using FsCheck;
using FsCheck.Fluent;

namespace DiffToJsonLib.Tests.Fuzzing;

public class RegexPiiRedactorFuzzTests
{
    [Test]
    public void Redact_NeverThrows_OnFsCheckGeneratedInputs()
    {
        RegexPiiRedactor redactor = new RegexPiiRedactor();

        Prop.ForAll(ArbMap.Default.GeneratorFor<string>().ToArbitrary(), input =>
        {
            string result = redactor.Redact(input ?? "");
            if (string.IsNullOrEmpty(result)) return false;

            ReadOnlySpan<char> sourceSpan = (input ?? "").AsSpan();
            Span<char> destSpan = stackalloc char[Math.Max(1024, (input ?? "").Length * 2)];
            int written = redactor.Redact(sourceSpan, destSpan);
            if (written < 0) return false;
            string spanResult = destSpan[..written].ToString();
            if (string.IsNullOrEmpty(spanResult)) return false;

            return true;
        }).Check(Config.Default.WithMaxTest(100));
    }

    [Test]
    public async Task Redact_NeverThrows_OnFixedInputs()
    {
        RegexPiiRedactor redactor = new RegexPiiRedactor();
        string[] strings =
        [
            "",
            "hello world",
            "user@example.com",
            "<admin@test.org>",
            new('a', 10000),
            "no emails here",
            "email: test@domain.com and another@foo.bar",
            "\0\x01\x02",
            "string with\nnewlines\tand\ttabs",
            "<a@b.c>",
            "a@b.c"
        ];

        foreach (string input in strings)
        {
            string result = redactor.Redact(input);
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
        string? redactedSpanResult = destSpan[..written].ToString();
        if (redactedSpanResult == null) throw new Exception("Redacted result cannot be null");
    }

    [Test]
    public async Task Redact_ReturnsNonEmpty_ForNonNullInput()
    {
        RegexPiiRedactor redactor = new RegexPiiRedactor();
        string[] nonEmptyInputs =
        [
            "hello",
            "test@email.com",
            "a@b.c",
            new('x', 1000)
        ];

        foreach (string input in nonEmptyInputs)
        {
            string result = redactor.Redact(input);
            await Assert.That(result).IsNotNull();
            await Assert.That(result).IsNotEmpty();
        }
    }

    [Test]
    public async Task Redact_Null_ReturnsEmpty()
    {
        RegexPiiRedactor redactor = new RegexPiiRedactor();
        string result = redactor.Redact(null);
        await Assert.That(result).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task Redact_RemovesEmailAddresses()
    {
        RegexPiiRedactor redactor = new RegexPiiRedactor();
        string result = redactor.Redact("Contact me at user@example.com or <admin@test.org>");
        await Assert.That(result).DoesNotContain("user@example.com");
        await Assert.That(result).DoesNotContain("admin@test.org");
        await Assert.That(result).Contains("REDACTED");
    }
}
