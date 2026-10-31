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

namespace DiffToJsonLib.Training;

public static partial class PromptSubstitutor
{
    public static string Substitute(string template, string diff, string commitMessage, string repoName, string license, string repoUrl)
    {
        ArgumentNullException.ThrowIfNull(template);

        StringBuilder? builder = null;
        int lastCopiedIndex = 0;

        foreach (ValueMatch match in PlaceholderPattern.EnumerateMatches(template))
        {
            if (!TryResolveReplacement(
                    template.AsSpan(match.Index + 1, match.Length - 2),
                    diff,
                    commitMessage,
                    repoName,
                    license,
                    repoUrl,
                    out string? replacement))
            {
                // Unknown placeholders are left untouched; their text is copied verbatim below.
                continue;
            }

            builder ??= new StringBuilder(template.Length + 64);

            builder.Append(template, lastCopiedIndex, match.Index - lastCopiedIndex);
            builder.Append(replacement);
            lastCopiedIndex = match.Index + match.Length;
        }

        if (builder is null)
        {
            // No known placeholder was substituted; return the template unchanged without allocating.
            return template;
        }

        builder.Append(template, lastCopiedIndex, template.Length - lastCopiedIndex);
        return builder.ToString();
    }

    private static bool TryResolveReplacement(
        ReadOnlySpan<char> name,
        string diff,
        string commitMessage,
        string repoName,
        string license,
        string repoUrl,
        out string? replacement)
    {
        if (name.Equals("diff", StringComparison.OrdinalIgnoreCase))
        {
            replacement = diff;
            return true;
        }

        if (name.Equals("commitMessage", StringComparison.OrdinalIgnoreCase))
        {
            replacement = commitMessage;
            return true;
        }

        if (name.Equals("repoName", StringComparison.OrdinalIgnoreCase))
        {
            replacement = repoName;
            return true;
        }

        if (name.Equals("license", StringComparison.OrdinalIgnoreCase))
        {
            replacement = license;
            return true;
        }

        if (name.Equals("repoUrl", StringComparison.OrdinalIgnoreCase))
        {
            replacement = repoUrl;
            return true;
        }

        replacement = null;
        return false;
    }

    [GeneratedRegex(@"\{(?<name>\w+)\}", RegexOptions.ExplicitCapture, 3000)]
    private static partial Regex PlaceholderPattern { get; }
}
