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

public partial class RegexPiiRedactor : Redactor
{
    private readonly Regex _regex;

    public RegexPiiRedactor()
    {
        _regex = MyRegex;
    }

    [GeneratedRegex(@"<([^>\s]+@[^>\s]+\.[^>\s]+)>|\b[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}\b",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture, 3000)]
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
        string redacted = Redact(source.ToString());
        redacted.AsSpan().CopyTo(destination);
        return redacted.Length;
    }
}
