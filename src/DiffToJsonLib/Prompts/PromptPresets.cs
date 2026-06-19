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

namespace DiffToJsonLib.Prompts;

/// <summary>
/// Provides built-in <see cref="PromptTemplate"/> presets and validation for placeholder tokens.
/// </summary>
public static partial class PromptPresets
{
    /// <summary>
    /// A style-neutral preset suitable for general commit-message generation.
    /// </summary>
    public static readonly PromptTemplate Default = new(
        "You are a software engineer. You write high-quality commit messages that follow best practices.",
        "Write a commit message for the diff in the repository '{repoName}': " + Environment.NewLine + "{diff}"
    );

    /// <summary>
    /// A preset that instructs the model to follow the Conventional Commits specification.
    /// </summary>
    public static readonly PromptTemplate Conventional = new(
        "You are a software engineer. You write commit messages that follow the Conventional Commits specification.",
        "Write a Conventional Commits-style commit message for the diff in '{repoName}': " + Environment.NewLine + "{diff}"
    );

    private static readonly Dictionary<string, PromptTemplate> _presets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["default"] = Default,
        ["conventional"] = Conventional,
    };

    private static readonly HashSet<string> _knownPlaceholders = new(StringComparer.OrdinalIgnoreCase)
    {
        "diff",
        "commitMessage",
        "repoName"
    };

    static PromptPresets()
    {
        ValidateTemplate(Default);
        ValidateTemplate(Conventional);
    }

    /// <summary>
    /// Returns the preset with the given name.
    /// </summary>
    /// <param name="name">The preset name (case-insensitive).</param>
    /// <returns>The matching <see cref="PromptTemplate"/>.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when <paramref name="name"/> is not a known preset.</exception>
    public static PromptTemplate Get(string name) =>
        _presets.TryGetValue(name, out var template)
            ? template
            : throw new KeyNotFoundException(
                $"Unknown prompt preset '{name}'. Valid presets: {string.Join(", ", _presets.Keys)}.");

    /// <summary>
    /// Checks whether a preset name is registered.
    /// </summary>
    public static bool IsValid(string name) => _presets.ContainsKey(name);

    /// <summary>
    /// Validates that every <c>{placeholder}</c> token in the template belongs to the known set.
    /// </summary>
    /// <param name="template">The template to validate.</param>
    /// <exception cref="PromptTemplateValidationException">
    /// Thrown when an unknown placeholder is found.
    /// </exception>
    public static void ValidateTemplate(PromptTemplate template)
    {
        Regex regex = PlaceholderPattern();

        foreach (string field in new[] { template.System, template.User })
        {
            if (string.IsNullOrEmpty(field))
            {
                continue;
            }

            MatchCollection matches = regex.Matches(field);
            foreach (Match match in matches)
            {
                string name = match.Groups[1].Value;
                if (!_knownPlaceholders.Contains(name))
                {
                    throw new PromptTemplateValidationException(
                        $"The prompt template contains an unknown placeholder '{{{name}}}'. " +
                        $"Valid placeholders: {string.Join(", ", _knownPlaceholders.Select(p => $"{{{p}}}"))}.");
                }
            }
        }
    }

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex PlaceholderPattern();
}
