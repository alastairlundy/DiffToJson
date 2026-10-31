/*
    Copyright 2026 Alastair Lundy

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
 */

using System.Text.RegularExpressions;
using Microsoft.Extensions.Compliance.Redaction;

namespace DiffToJsonLib.Redactors;

public sealed partial class RegexPiiRedactor : Redactor
{
    private readonly Regex _regex;

    public RegexPiiRedactor()
    {
        _regex = MyRegex;
    }

    [GeneratedRegex(@"<([^>\s]+@[^>\s]+\.[^>\s]+)>|\b[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}\b",
        RegexOptions.ExplicitCapture, 3000)]
    private static partial Regex MyRegex { get; }

    public override string Redact(string? source)
    {
        if (source == null) 
            return string.Empty;
        
        return _regex.Replace(source, "REDACTED");
    }

    public override int GetRedactedLength(ReadOnlySpan<char> source)
    {
        // Heuristic: the fixed "REDACTED" replacement length; actual match length varies.
        return "REDACTED".Length;
    }

    public override int Redact(ReadOnlySpan<char> source, Span<char> destination)
    {
        ReadOnlySpan<char> replacement = "REDACTED";
        int written = 0;
        int sourceStart = 0;

        // Enumerate matches straight over the source span (amortized allocation free) and write the
        // redacted output directly into the destination buffer, replacing every match with "REDACTED"
        // exactly as the string-based overload does.
        foreach (ValueMatch match in _regex.EnumerateMatches(source))
        {
            written = Append(source.Slice(sourceStart, match.Index - sourceStart), destination, written);
            written = Append(replacement, destination, written);
            sourceStart = match.Index + match.Length;
        }

        return Append(source.Slice(sourceStart), destination, written);

        static int Append(ReadOnlySpan<char> value, Span<char> target, int charsWritten)
        {
            if (value.Length > target.Length - charsWritten)
            {
                // The Redactor contract documents ArgumentException when destination is too small.
                throw new ArgumentException("Destination is too short.", nameof(destination));
            }

            value.CopyTo(target.Slice(charsWritten));
            return charsWritten + value.Length;
        }
    }
}
