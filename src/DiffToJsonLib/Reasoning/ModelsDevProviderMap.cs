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

namespace DiffToJsonLib.Reasoning;

internal static class ModelsDevProviderMap
{
    private static readonly IReadOnlyDictionary<string, string> ProviderMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["openai"] = "openai",
            ["openai-compatible"] = "openai",
            ["anthropic"] = "anthropic",
            ["anthropic-compatible"] = "anthropic",
            ["openrouter"] = "openrouter",
            ["ollama"] = "ollama-cloud",
            ["ollama-cloud"] = "ollama-cloud",
            ["minimax"] = "minimax",
            ["deepseek"] = "deepseek",
        };

    public static bool TryGetProviderId(string cliProviderName, out string? modelsDevProviderId)
    {
        if (string.IsNullOrWhiteSpace(cliProviderName))
        {
            modelsDevProviderId = string.Empty;
            return false;
        }

        return ProviderMap.TryGetValue(cliProviderName.Trim(), out modelsDevProviderId);
    }

    /// <summary>
    /// Returns the canonical models.dev provider id for dispatch, falling back to
    /// the trimmed lower-case CLI value when unmapped.
    /// </summary>
    public static string Normalize(string cliProviderName)
    {
        if (string.IsNullOrWhiteSpace(cliProviderName))
            return string.Empty;

        string trimmed = cliProviderName.Trim();
        return ProviderMap.TryGetValue(trimmed, out string? canonical)
            ? canonical
            : trimmed.ToLowerInvariant();
    }
}
